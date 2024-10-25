using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlatformTools : MonoBehaviour
{
    public float height = 1.0f;
    public float reducePointsTo = 0.9f;
    private Vector3[] pointsOriginal;
    private Vector3[] rightTangentOriginal;
    private Vector3[] leftTangentOriginal;
    private float[] heightOriginal;
    private UnityEngine.U2D.ShapeTangentMode[] modeOriginal;
    
    private Vector3[] pointsOriginalReal;
    private Vector3[] rightTangentOriginalReal;
    private Vector3[] leftTangentOriginalReal;
    private float[] heightOriginalReal;
    private UnityEngine.U2D.ShapeTangentMode[] modeOriginalReal;

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

    void SaveAbsoluteOriginal()
    {
        if (pointsOriginalReal == null) {
            var spriteShapeController = GetComponent<UnityEngine.U2D.SpriteShapeController>();
            int pointsNum = spriteShapeController.spline.GetPointCount();

            pointsOriginalReal = new Vector3[pointsNum];
            rightTangentOriginalReal = new Vector3[pointsNum];
            leftTangentOriginalReal = new Vector3[pointsNum];
            heightOriginalReal = new float[pointsNum];
            modeOriginalReal = new UnityEngine.U2D.ShapeTangentMode[pointsNum];

            for (int i = 0; i < pointsNum; ++i) {
                pointsOriginalReal[i] = spriteShapeController.spline.GetPosition(i);
                rightTangentOriginalReal[i] = spriteShapeController.spline.GetRightTangent(i);
                leftTangentOriginalReal[i] = spriteShapeController.spline.GetLeftTangent(i);
                heightOriginalReal[i] = spriteShapeController.spline.GetHeight(i);
                modeOriginalReal[i] = spriteShapeController.spline.GetTangentMode(i);
            }
        }
    }

    [ContextMenu("UpdateHeight")]
    void UpdateHeight()
    {
        SaveAbsoluteOriginal();

        var spriteShapeController = GetComponent<UnityEngine.U2D.SpriteShapeController>();
        int pointsNum = spriteShapeController.spline.GetPointCount();
        for (int i = 0; i < pointsNum; ++i) {
            int right = i == pointsNum - 1 ? 0 : i + 1;
            int left = i == 0 ? pointsNum - 1 : i - 1;

            Vector3 pointRight = CalculateBezierPoint(
                0.01f, 
                spriteShapeController.spline.GetPosition(i), 
                spriteShapeController.spline.GetRightTangent(i) + spriteShapeController.spline.GetPosition(i), 
                spriteShapeController.spline.GetLeftTangent(right) + spriteShapeController.spline.GetPosition(right), 
                spriteShapeController.spline.GetPosition(right));

            Vector3 pointLeft = CalculateBezierPoint(
                1f - 0.01f, 
                spriteShapeController.spline.GetPosition(left), 
                spriteShapeController.spline.GetRightTangent(left) + spriteShapeController.spline.GetPosition(left), 
                spriteShapeController.spline.GetLeftTangent(i) + spriteShapeController.spline.GetPosition(i), 
                spriteShapeController.spline.GetPosition(i));

            Vector3 firstDerivativeApprox = (pointRight - pointLeft).normalized;
            Vector3 normal = Vector2.Perpendicular(firstDerivativeApprox).normalized;

            float heightDiff = height - spriteShapeController.spline.GetHeight(i);
            spriteShapeController.spline.SetHeight(i, height);
            spriteShapeController.spline.SetPosition(i, spriteShapeController.spline.GetPosition(i) - normal * heightDiff / 4f);
        }
    }

    float CollinearityValue(Vector3 a, Vector3 b, Vector3 c) 
    {
        // Vector3 scale = new Vector3(Mathf.Max(a.x, b.x, c.x), Mathf.Max(a.y, b.y, c.y), Mathf.Max(a.z, b.z, c.z));
        // a = Vector3.Scale(a, scale);
        // b = Vector3.Scale(b, scale);
        // c = Vector3.Scale(c, scale);
        Vector2 arg1 = b - a;
        Vector2 arg2 = c - a;
        return Mathf.Abs(arg1.x * arg2.y - arg1.y * arg2.x);
    }

    float CollinearityValueMultiple(Vector3[] vectors) 
    {
        float totalValue = 0.0f;
        for (int i = 1; i < vectors.Length - 1; ++i) {
            int left = i - 1;
            int right = i + 1;
            totalValue += CollinearityValue(vectors[left], vectors[i], vectors[right]);
        }
        return totalValue;
    }

    [ContextMenu("DropOrignal")]
    void ResetOrignalPoints()
    {
        pointsOriginal = null;
    }

    [ContextMenu("RestoreOrignal")]
    void RestoreOrignalPoints()
    {
        if (pointsOriginal != null) {
            var spriteShapeController = GetComponent<UnityEngine.U2D.SpriteShapeController>();
            spriteShapeController.spline.Clear();

            for (int i = 0; i < pointsOriginal.Length; ++i) {
                spriteShapeController.spline.InsertPointAt(i,  pointsOriginal[i]);
                spriteShapeController.spline.SetTangentMode(i, UnityEngine.U2D.ShapeTangentMode.Continuous);
                spriteShapeController.spline.SetRightTangent(i, rightTangentOriginal[i]);
                spriteShapeController.spline.SetLeftTangent(i, leftTangentOriginal[i]);
                spriteShapeController.spline.SetHeight(i, heightOriginal[i]);
            }
        }
    }

    [ContextMenu("RestoreAbsoluteOrignal")]
    void RestoreOrignalRealPoints()
    {
        if (pointsOriginal != null) {
            var spriteShapeController = GetComponent<UnityEngine.U2D.SpriteShapeController>();
            spriteShapeController.spline.Clear();

            for (int i = 0; i < pointsOriginal.Length; ++i) {
                spriteShapeController.spline.InsertPointAt(i,  pointsOriginalReal[i]);
                spriteShapeController.spline.SetTangentMode(i, UnityEngine.U2D.ShapeTangentMode.Continuous);
                spriteShapeController.spline.SetRightTangent(i, rightTangentOriginalReal[i]);
                spriteShapeController.spline.SetLeftTangent(i, leftTangentOriginalReal[i]);
                spriteShapeController.spline.SetHeight(i, heightOriginalReal[i]);
            }
        }
    }

    [ContextMenu("Simplify")]
    void Simplify()
    {
        SaveAbsoluteOriginal();
        
        var spriteShapeController = GetComponent<UnityEngine.U2D.SpriteShapeController>();
        int pointsNum = spriteShapeController.spline.GetPointCount();

        if (pointsOriginal == null) {
            pointsOriginal = new Vector3[pointsNum];
            rightTangentOriginal = new Vector3[pointsNum];
            leftTangentOriginal = new Vector3[pointsNum];
            heightOriginal = new float[pointsNum];
            modeOriginal = new UnityEngine.U2D.ShapeTangentMode[pointsNum];

            for (int i = 0; i < pointsNum; ++i) {
                pointsOriginal[i] = spriteShapeController.spline.GetPosition(i);
                rightTangentOriginal[i] = spriteShapeController.spline.GetRightTangent(i);
                leftTangentOriginal[i] = spriteShapeController.spline.GetLeftTangent(i);
                heightOriginal[i] = spriteShapeController.spline.GetHeight(i);
                modeOriginal[i] = spriteShapeController.spline.GetTangentMode(i);
            }
        }

        int targetNumPoints = Mathf.Max((int)Mathf.Ceil(pointsNum * reducePointsTo), 3);
        
        while (pointsNum > targetNumPoints) {
            float? minValue = null;
            int index = -1;
            for (int i = 0; i < pointsNum; ++i) {
                int right = i == pointsNum - 1 ? 0 : i + 1;
                int left = i == 0 ? pointsNum - 1 : i - 1;

                float value = CollinearityValueMultiple(new [] {
                    spriteShapeController.spline.GetPosition(left),
                    spriteShapeController.spline.GetRightTangent(left) + spriteShapeController.spline.GetPosition(left),
                    spriteShapeController.spline.GetPosition(i), 
                    spriteShapeController.spline.GetLeftTangent(right) + spriteShapeController.spline.GetPosition(right), 
                    spriteShapeController.spline.GetPosition(right)
                });
                
                if (minValue == null || value < minValue) {
                    minValue = value;
                    index = i;
                }
            }

            int indexRight = index == pointsNum - 1 ? 0 : index + 1;
            int indexLeft = index == 0 ? pointsNum - 1 : index - 1;

            float t = spriteShapeController.spline.GetLeftTangent(index).magnitude  /
                (spriteShapeController.spline.GetLeftTangent(index) - spriteShapeController.spline.GetRightTangent(index)).magnitude;
            spriteShapeController.spline.SetRightTangent(
                indexLeft,
                spriteShapeController.spline.GetRightTangent(indexLeft) / t);
            spriteShapeController.spline.SetLeftTangent(
                indexRight,
                spriteShapeController.spline.GetLeftTangent(indexRight) / (1 - t));

            spriteShapeController.spline.RemovePointAt(index);

            pointsNum -= 1;
        }
    }
}