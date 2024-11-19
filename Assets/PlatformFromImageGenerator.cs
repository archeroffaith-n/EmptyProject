using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlatformFromImageGenerator : MonoBehaviour
{
    public string scriptPath = "Assets/image2platform.py";
    public UnityEngine.U2D.SpriteShape spriteShapeProfile;
    public string imagePath;
    public int grayThreshold = 110;
    public float criticalDrop = 0.003f;
    public float scale = 100f;
    public bool scaleFix = true;
    public float minArea = 0.001f;
    public float height = 0.1f;

    private GameObject[] platforms;


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

    [ContextMenu("GeneratePlatforms")]
    void GeneratePlatforms()
    {
        var p = new System.Diagnostics.Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;

        p.StartInfo.FileName = "cmd.exe";
        p.StartInfo.Arguments = String.Format("/C python {0} {1} {2} {3} {4} {5}",
            scriptPath, imagePath, grayThreshold, 
            criticalDrop.ToString("0.000000000", System.Globalization.CultureInfo.InvariantCulture), 
            scaleFix ? 1 : 0, 
            minArea.ToString("0.000000000", System.Globalization.CultureInfo.InvariantCulture));

        p.Start();
        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();

        string lastErrorLine = null;
        while (!p.StandardError.EndOfStream)
        {
            lastErrorLine = p.StandardError.ReadLine();
        }
        if (lastErrorLine != null && lastErrorLine.ToLower().Contains("error"))
        {
            throw new Exception(lastErrorLine);
        }

        // Debug.Log(output);

        string[] rows = output.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        int numPlatforms = Int32.Parse(rows[0]);
        platforms = new GameObject[numPlatforms];

        int lastRow = 1;
        for (int i = 0; i < numPlatforms; ++i)
        {
            int numPoints = Int32.Parse(rows[lastRow++]);
            string[] values = rows[lastRow++].Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
            Vector3 center = new Vector3(
                float.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture) * scale,
                float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture) * scale,
                0);

            platforms[i] = new GameObject(System.String.Format("Platform {0}", i), typeof(UnityEngine.U2D.SpriteShapeController));
            platforms[i].transform.SetParent(gameObject.transform, false);
            platforms[i].transform.position = center;
            var spriteShapeController = platforms[i].GetComponent<UnityEngine.U2D.SpriteShapeController>();
            spriteShapeController.spline.RemovePointAt(0);
            spriteShapeController.spline.isOpenEnded = false;
            spriteShapeController.spline.Clear();
            spriteShapeController.fillPixelsPerUnit = 256;
            spriteShapeController.spriteShape = spriteShapeProfile;

            // spriteShapeController.spriteShape = new UnityEngine.U2D.SpriteShape();

            // Vector3 points;
            Vector3 point;
            Vector3 tangentRight;
            Vector3 tangentLeft;
            for (int j = 0; j < numPoints; ++j)
            {
                values = rows[lastRow + j].Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                point = new Vector3(
                    float.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture) * scale,
                    float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture) * scale,
                    0);
                tangentRight = new Vector3(
                    float.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture) * scale,
                    float.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture) * scale,
                    0) - point;
                tangentLeft = new Vector3(
                    float.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture) * scale,
                    float.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture) * scale,
                    0) - point;

                spriteShapeController.spline.InsertPointAt(j, point - center);
                spriteShapeController.spline.SetTangentMode(j, UnityEngine.U2D.ShapeTangentMode.Continuous);
                spriteShapeController.spline.SetRightTangent(j, tangentRight);
                spriteShapeController.spline.SetLeftTangent(j, tangentLeft);
                spriteShapeController.spline.SetHeight(j, 0.1f);
            }

            spriteShapeController.RefreshSpriteShape();
            spriteShapeController.BakeMesh();

            lastRow += numPoints;
        }

        for (int index = 0; index < numPlatforms; ++index)
        {
            var spriteShapeController = platforms[index].GetComponent<UnityEngine.U2D.SpriteShapeController>();
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

                spriteShapeController.spline.SetHeight(i, height);
                spriteShapeController.spline.SetPosition(i, spriteShapeController.spline.GetPosition(i) - normal * height / 4f);
            }
        }
    }
}