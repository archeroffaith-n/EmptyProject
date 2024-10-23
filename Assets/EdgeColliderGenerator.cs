using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EdgeColliderGenerator : MonoBehaviour
{
    public float heightFix = 0;
    public int pointsQuantity = 1000;
    public float minRadius = 0.3f;
    public float maxRadius = 1.0f;
    public float criticalDrop = 0.03f;
    public int numClusters = 5;

    private Vector3 scale;
    private Vector3 reverseScale;

    Vector3[] points;
    Vector3[] tangents;
    Vector3[] normals;
    float[] curvatures;
    float[] radiuses;
    float[] maxRadiuses;
    float[] firstHeights;
    float[] secondHeights;
    float[] lengths;
    float[] lengthsTotal;

    Vector3[] selectedPoints;
    Vector3[] selectedNormals;
    float[] selectedMaxRadiuses;
    bool[] selectedPositive;

    UnityEngine.U2D.SpriteShapeController spriteShapeController;
    GameObject[] colliderObjects;

    Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 handlerP0, Vector2 handlerP1, Vector2 p1)
    {
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector2 p = uuu * p0;
        p += 3f * uu * t * handlerP0;
        p += 3f * u * tt * handlerP1;
        p += ttt * p1;

        return p;
    }

    Vector2 CalculateBezierFirstDerivative(float t, Vector2 p0, Vector2 handlerP0, Vector2 handlerP1, Vector2 p1)
    {
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;

        Vector2 p = -3f * uu * p0;
        p += -3f * (2f * u * t - uu) * handlerP0;
        p += -3f * (tt - 2f * u * t) * handlerP1;
        p += 3f * tt * p1;

        return p;
    }

    Vector2 CalculateBezierSecondDerivative(float t, Vector2 p0, Vector2 handlerP0, Vector2 handlerP1, Vector2 p1)
    {
        float u = 1.0f - t;

        Vector2 p = -6f * u * p0;
        p += 6f * t * handlerP0;
        p += -3f * (4f * t - 2f * u) * handlerP1;
        p += 6f * t * p1;

        return p;
    }

    float InterpolateLinear(float t, float a, float b)
    {
        return Mathf.Lerp(a, b, t);
    }

    float InterpolateSmooth(float t, float a, float b)
    {
        float mu2 = (1.0f - Mathf.Cos(t * Mathf.PI)) / 2.0f;
        return (a * (1 - mu2) + b * mu2);
    }

    float CalculateArea(Vector3[] pointsArray)
    {
        int pointsNum = pointsArray.Length;
        float area = 0.0f;
        for (int i = 0; i < pointsNum; ++i)
        {
            int right = i == pointsNum - 1 ? 0 : i + 1;

            var point1 = pointsArray[i];
            var point2 = pointsArray[right];

            area += (point1.x * point2.y - point2.x * point1.y) / 2.0f;
        }

        return -area;
    }

    Vector3 anchorPoint;
    Vector3 badPoint;
    Vector3 mirroredBadPoint;
    Vector3 resCenter;

    void CalculateShape()
    {
        int pointsNum = spriteShapeController.spline.GetPointCount();
        Vector3[] splinePoints = new Vector3[pointsNum];
        for (int i = 0; i < pointsNum; i++)
        {
            splinePoints[i] = spriteShapeController.spline.GetPosition(i);
        }

        float normalCoefficient = 1.0f;
        if (CalculateArea(splinePoints) < 0)
        {
            normalCoefficient = -1.0f;
        }

        points = new Vector3[pointsNum * pointsQuantity];
        tangents = new Vector3[pointsNum * pointsQuantity];
        normals = new Vector3[pointsNum * pointsQuantity];
        curvatures = new float[pointsNum * pointsQuantity];
        radiuses = new float[pointsNum * pointsQuantity];
        maxRadiuses = new float[pointsNum * pointsQuantity];
        firstHeights = new float[pointsNum * pointsQuantity];
        secondHeights = new float[pointsNum * pointsQuantity];
        lengths = new float[pointsNum * pointsQuantity + 1];
        lengthsTotal = new float[pointsNum];

        // Координаты
        for (int i = 0; i < pointsNum; i++)
        {
            int right = i == pointsNum - 1 ? 0 : i + 1;
            Vector3 firstPoint = spriteShapeController.spline.GetPosition(i);
            Vector3 secondPoint = spriteShapeController.spline.GetPosition(right);
            Vector3 handlerFirstPoint = spriteShapeController.spline.GetRightTangent(i) + firstPoint;
            Vector3 handlerSecondPoint = spriteShapeController.spline.GetLeftTangent(right) + secondPoint;
            float firstHeight = spriteShapeController.spline.GetHeight(i) / 4f;
            float secondHeight = spriteShapeController.spline.GetHeight(right) / 4f;

            for (int j = 0; j < pointsQuantity; j++)
            {
                int index = i * pointsQuantity + j;
                Vector3 point = CalculateBezierPoint(1f / pointsQuantity * j, firstPoint, handlerFirstPoint, handlerSecondPoint, secondPoint);
                Vector2 tangent = CalculateBezierFirstDerivative(1f / pointsQuantity * j, firstPoint, handlerFirstPoint, handlerSecondPoint, secondPoint).normalized;
                Vector3 normal = Vector2.Perpendicular(tangent).normalized * normalCoefficient;
                points[index] = point;
                normals[index] = normal;
                firstHeights[index] = firstHeight;
                secondHeights[index] = secondHeight;
            }
        }

        // Длины
        for (int i = 1; i < points.Length; ++i)
        {
            int left = i - 1;
            lengths[i] = lengths[left] + (points[i] - points[left]).magnitude;

            if (i % pointsQuantity == 0)
            {
                lengthsTotal[i / pointsQuantity - 1] = lengths[i];
            }
        }
        lengths[points.Length] = lengths[points.Length - 1] + (points[0] - points[points.Length - 1]).magnitude;
        lengthsTotal[pointsNum - 1] = lengths[points.Length];

        for (int i = pointsNum - 1; i > 0; --i)
        {
            lengthsTotal[i] -= lengthsTotal[i - 1];
        }

        // Обновление координат
        for (int i = 0; i < points.Length; ++i)
        {
            float height = InterpolateSmooth(
                (lengths[i] - lengths[i / pointsQuantity * pointsQuantity]) / lengthsTotal[i / pointsQuantity],
                firstHeights[i],
                secondHeights[i]);
            points[i] += normals[i] * (height + heightFix);

            points[i] = Vector3.Scale(points[i], scale);
        }

        // Обновление длин
        for (int i = 1; i < points.Length; ++i)
        {
            int left = i - 1;
            lengths[i] = lengths[left] + (points[i] - points[left]).magnitude;

            if (i % pointsQuantity == 0)
            {
                lengthsTotal[i / pointsQuantity - 1] = lengths[i];
            }
        }
        lengths[points.Length] = lengths[points.Length - 1] + (points[0] - points[points.Length - 1]).magnitude;
        lengthsTotal[pointsNum - 1] = lengths[points.Length];

        for (int i = pointsNum - 1; i > 0; --i)
        {
            lengthsTotal[i] -= lengthsTotal[i - 1];
        }

        // Касательные и нормали
        for (int i = 0; i < points.Length; ++i)
        {
            int left = i == 0 ? points.Length - 1 : i - 1;
            int right = i == points.Length - 1 ? 0 : i + 1;

            Vector3 firstDerivativeApprox = (points[right] - points[left]).normalized;
            Vector3 normal = Vector2.Perpendicular(firstDerivativeApprox).normalized * normalCoefficient;

            tangents[i] = firstDerivativeApprox;
            normals[i] = normal;
        }

        // Кривизна
        for (int i = 0; i < points.Length; ++i)
        {
            int left = i == 0 ? points.Length - 1 : i - 1;
            int right = i == points.Length - 1 ? 0 : i + 1;

            Vector3 secondDerivativeApprox = (tangents[right] - tangents[left]) / (points[right] - points[left]).magnitude;
            float curvature = -secondDerivativeApprox.magnitude * Mathf.Sign(Vector3.Dot(secondDerivativeApprox, normals[i]));
            if (curvature > 0)
            {
                curvature = Mathf.Min(1 / minRadius, curvature);
            }

            curvatures[i] = curvature;

            curvature = Mathf.Max(curvatures[i], 1e-5f);

            radiuses[i] = Mathf.Max(1 / curvatures[i], minRadius);
        }

        // Проверка, что радиусы не выходят за границы в других точках
        float lastPositiveRadius = 1e7f;
        for (int i = 0; i < points.Length; ++i)
        {
            if (radiuses[i] > 0)
            {
                lastPositiveRadius = radiuses[i];
            }
        }
        for (int i = 0; i < points.Length; ++i)
        {
            if (radiuses[i] > 0)
            {
                lastPositiveRadius = radiuses[i];
            }
            maxRadiuses[i] = radiuses[i];
            if (maxRadiuses[i] < 0)
            {
                maxRadiuses[i] = lastPositiveRadius;
            }
            Vector3 center = points[i] - normals[i] * maxRadiuses[i];
            for (int j = 0; j < points.Length; ++j)
            {
                if (j == i)
                {
                    continue;
                }
                if ((points[j] - center).magnitude >= maxRadiuses[i])
                {
                    continue;
                }

                Vector3 mirroredPoint = points[j] - 2.0f * (points[j] - points[i] - Vector3.Dot(points[j] - points[i], normals[i]) * normals[i]);

                center = new Vector3(
                    0.5f * ((points[i].x * points[i].x + points[i].y * points[i].y) * (points[j].y - mirroredPoint.y)
                            + (points[j].x * points[j].x + points[j].y * points[j].y) * (mirroredPoint.y - points[i].y)
                            + (mirroredPoint.x * mirroredPoint.x + mirroredPoint.y * mirroredPoint.y) * (points[i].y - points[j].y))
                         / (points[i].x * (points[j].y - mirroredPoint.y)
                            + points[j].x * (mirroredPoint.y - points[i].y)
                            + mirroredPoint.x * (points[i].y - points[j].y)),
                    -0.5f * ((points[i].x * points[i].x + points[i].y * points[i].y) * (points[j].x - mirroredPoint.x)
                            + (points[j].x * points[j].x + points[j].y * points[j].y) * (mirroredPoint.x - points[i].x)
                            + (mirroredPoint.x * mirroredPoint.x + mirroredPoint.y * mirroredPoint.y) * (points[i].x - points[j].x))
                         / (points[i].x * (points[j].y - mirroredPoint.y)
                            + points[j].x * (mirroredPoint.y - points[i].y)
                            + mirroredPoint.x * (points[i].y - points[j].y)),
                    points[i].z);

                maxRadiuses[i] = (points[i] - center).magnitude;
            }
            maxRadiuses[i] = Mathf.Max(Mathf.Min(maxRadiuses[i], maxRadius), minRadius);
        }



        // Отбор такого минимального числа точек, чтобы достаточно точно описывать кривую
        List<int> selectedIndices = new List<int>();
        int selectedIndex = 0;
        while (true)
        {
            selectedIndices.Add(selectedIndex);
            int j = selectedIndex + 1;
            for (; j < points.Length + 1; ++j)
            {
                if ((j == points.Length) || (Mathf.Abs(Vector3.Dot(normals[selectedIndex], points[j] - points[selectedIndex])) > criticalDrop))
                {
                    break;
                }
            }
            if (j == points.Length)
            {
                break;
            }
            selectedIndex = j;
        }

        int numSelectedPoints = selectedIndices.Count;

        selectedPoints = new Vector3[numSelectedPoints];
        selectedNormals = new Vector3[numSelectedPoints];
        selectedMaxRadiuses = new float[numSelectedPoints];
        selectedPositive = new bool[numSelectedPoints];

        foreach (var (value, i) in selectedIndices.Select((value, i) => (value, i)))
        {
            selectedPoints[i] = points[value];
            selectedNormals[i] = normals[value];
            selectedMaxRadiuses[i] = maxRadiuses[value];
            selectedPositive[i] = radiuses[value] > 0;
        }

        // Кластеризация точек по радиусам, чтобы использовать наименьшее количество разнообразных радиусов
        int[] clusterId = new int[numSelectedPoints];
        float[] clusterValue = new float[numSelectedPoints];
        for (int i = 0; i < numSelectedPoints; ++i)
        {
            clusterId[i] = i;
            clusterValue[i] = selectedMaxRadiuses[i];
        }

        for (int numMerges = 0; numMerges < numSelectedPoints - numClusters; ++numMerges)
        {
            System.Tuple<int, int> bestMergePair = null;
            float bestDiff = 1e9f;
            for (int i = 0; i < numSelectedPoints; ++i)
            {
                int left = i == 0 ? numSelectedPoints - 1 : i - 1;
                if (clusterId[i] != clusterId[left])
                {
                    float diff = (clusterValue[i] - clusterValue[left]) / Mathf.Min(clusterValue[i], clusterValue[left]);
                    if (bestDiff > Mathf.Abs(diff))
                    {
                        bestDiff = Mathf.Abs(diff);
                        if (diff >= 0)
                        {
                            bestMergePair = System.Tuple.Create(clusterId[left], clusterId[i]);
                        }
                        else
                        {
                            bestMergePair = System.Tuple.Create(clusterId[i], clusterId[left]);
                        }
                    }
                }
            }

            if (bestMergePair == null)
            {
                throw new System.Exception("No possible merges found");
            }

            for (int i = 0; i < numSelectedPoints; ++i)
            {
                if (clusterId[i] == bestMergePair.Item2)
                {
                    clusterId[i] = bestMergePair.Item1;
                    if (clusterValue[bestMergePair.Item1] > clusterValue[i])
                    {
                        Debug.Log("Smth wrong");
                    }
                    clusterValue[i] = clusterValue[bestMergePair.Item1];
                }
            }
        }

        for (int i = 0; i < numSelectedPoints; ++i)
        {
            selectedMaxRadiuses[i] = clusterValue[i];
        }
    }

    [ContextMenu("CreateCollider")]
    void CreateCollider()
    {
        spriteShapeController = GetComponent<UnityEngine.U2D.SpriteShapeController>();
        if (spriteShapeController == null)
        {
            throw new System.Exception("SpriteShapeController component is required to calculate collider");
        }

        if (colliderObjects != null)
        {
            foreach (var colliderObject in colliderObjects)
            {
                DestroyImmediate(colliderObject);
            }
        }
        colliderObjects = new GameObject[numClusters];

        scale = transform.lossyScale;
        reverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);

        CalculateShape();

        int startIndex = 0;
        for (int i = 0; i < selectedPoints.Length; ++i)
        {
            int left = i == 0 ? selectedPoints.Length - 1 : i - 1;
            if (selectedMaxRadiuses[i] != selectedMaxRadiuses[left])
            {
                startIndex = i;
                break;
            }
        }

        List<float> colliderRadiuses = new List<float>();
        List<List<Vector2>> colliderPoints = new List<List<Vector2>>();
        for (int i = 0; i < selectedPoints.Length; ++i)
        {
            int index = startIndex + i;
            if (index >= selectedPoints.Length)
            {
                index -= selectedPoints.Length;
            }
            int left = index == 0 ? selectedPoints.Length - 1 : index - 1;
            int right = index == selectedPoints.Length - 1 ? 0 : index + 1;

            if (selectedMaxRadiuses[index] != selectedMaxRadiuses[left])
            {
                colliderRadiuses.Add(selectedMaxRadiuses[index]);
                colliderPoints.Add(new List<Vector2>());
            }

            if (selectedMaxRadiuses[index] < selectedMaxRadiuses[left])
            {
                colliderPoints.Last().Add(Vector3.Scale(selectedPoints[left] - selectedNormals[left] * selectedMaxRadiuses[index], reverseScale));
            }

            colliderPoints.Last().Add(Vector3.Scale(selectedPoints[index] - selectedNormals[index] * selectedMaxRadiuses[index], reverseScale));

            if (selectedMaxRadiuses[index] < selectedMaxRadiuses[right])
            {
                colliderPoints.Last().Add(Vector3.Scale(selectedPoints[right] - selectedNormals[right] * selectedMaxRadiuses[index], reverseScale));
            }
        }

        if (colliderPoints.Count != numClusters)
        {
            throw new System.Exception("Bad resulting num clusters");
        }


        for (int i = 0; i < numClusters; ++i)
        {
            colliderObjects[i] = new GameObject(System.String.Format("Edge Collider {0}", i), typeof(EdgeCollider2D));
            colliderObjects[i].transform.SetParent(gameObject.transform, false);
            var edgeCollider = colliderObjects[i].GetComponent<EdgeCollider2D>();
            edgeCollider.SetPoints(colliderPoints.ElementAt(i));
            edgeCollider.edgeRadius = colliderRadiuses.ElementAt(i);
        }
    }
}
