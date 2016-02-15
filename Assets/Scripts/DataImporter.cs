using UnityEngine;
using System.Collections.Generic;

public class DataImporter {

    public static bool Warp { get; set; }
    public static float WarpRadius { get; set; }
    public static float WarpLength { get; set; }
    public static float WarpFactor { get; set; }

    //Controls how far behind intersections are pushed
    private static float PushFactor = -0.1f;

    private static System.Random rnd = new System.Random();

    //Encoding in vector2 for convenience. First dimension is time and second is height.
    public static KeyValuePair<Vector2, Vector2>[] ParsePostions(string[] line)
    {
        List<KeyValuePair<Vector2, Vector2>> res = new List<KeyValuePair<Vector2, Vector2>>();
        float height;
        bool wasNumber = float.TryParse(line[1], out height);
        float lastHeight = wasNumber ? height : Mathf.PI;//Picked PI to flag an invalid number
        Vector2 start = wasNumber ? new Vector2(0, height) : Vector2.zero;
        //The first column is used for the name
        for (int i = 2; i < line.Length; i++)
        {
            bool number = float.TryParse(line[i], out height);
            //Beginning of path
            if (!wasNumber && number)
            {
                start = new Vector2(i - 1, height);
                lastHeight = height;
            }
            else
            {
                //End of path
                if (wasNumber && !number)
                {
                    res.Add(new KeyValuePair<Vector2, Vector2>(start, new Vector2(i - 1, lastHeight)));
                    lastHeight = Mathf.PI;
                }
                else
                {
                    //Change in height
                    if (wasNumber && number && lastHeight != height)
                    {
                        res.Add(new KeyValuePair<Vector2, Vector2>(start, new Vector2(i - 1, lastHeight)));
                        lastHeight = height;
                        start = new Vector2(i - 1, height);
                    }
                }
            }
            wasNumber = number;
        }
        //Edge case
        if (wasNumber)
            res.Add(new KeyValuePair<Vector2, Vector2>(start, new Vector2(line.Length - 2, lastHeight)));
        return res.ToArray();
    }

    //Adds a bezier curved path between two middle points
    public static void AddBezierCurve(Vector3 start, Vector3 end, float lineWidth, float curveSteps, List<Vector3> verts, List<Vector3> norms, List<int> tris, ref int baseIndex, float curveOffset)
    {
        float offset = 1.0f + (float)rnd.NextDouble();
        float backFactor = offset * PushFactor;
        start.z -= backFactor;
        end.z -= backFactor;

        //Calculate the two starting points
        Vector3 dir = lineWidth * Vector3.right;
        Vector3 cw = new Vector3(dir.y, -dir.x, dir.z);
        Vector3 ccw = new Vector3(-dir.y, dir.x, dir.z);
        //Lower left
        Vector3 lowPrev = start + cw;
        Vector3 highPrev = start + ccw;

        //Calculate the Bezier offset points
        Vector3 startOffset = start + Vector3.right * curveOffset;
        Vector3 endOffset = end + Vector3.left * curveOffset;
        
        float t = curveSteps;        
        bool first = true;
        while (t <= 1)
        {
            Vector3 next = Mathf.Pow(1.0f - t, 3) * start +
                          3.0f * Mathf.Pow(1.0f - t, 2) * t * startOffset +
                          3.0f * (1.0f - t) * t * t * endOffset +
                          Mathf.Pow(t, 3) * end;
            Vector3 tangent = 3.0f * Mathf.Pow(1.0f - t, 2) * (startOffset - start) +
                              6.0f * (1.0f - t) * t * (endOffset - startOffset) +
                              3.0f * t * t * (end - endOffset);
            tangent.Normalize();
            tangent *= lineWidth;
            cw = new Vector3(tangent.y, -tangent.x, tangent.z);
            ccw = new Vector3(-tangent.y, tangent.x, tangent.z);
            Vector3 lowNext = next + cw;
            Vector3 highNext = next + ccw;
            t += curveSteps;

            //Make the curves pop out
            if (first)
            {
                lowPrev.z += backFactor;
                highPrev.z += backFactor;
                first = false;
            }
            if (t > 1)
            {
                highNext.z += backFactor;
                lowNext.z += backFactor;
            }
            AddQuad(lowPrev, highNext, lowNext, highPrev, verts, norms, tris, ref baseIndex);
            lowPrev = lowNext;
            highPrev = highNext;
        }        
        start.z += backFactor;
        end.z += backFactor;        
    }

    //Ads a rectangular segment between two middle points
    public static void AddStraightPath(Vector3 start, Vector3 end, float lineWidth, List<Vector3> verts, List<Vector3> norms, List<int> tris, ref int baseIndex)
    {
        //Aux vectors
        Vector3 dir = end - start;
        dir.Normalize();
        dir *= lineWidth;
        Vector3 cw = new Vector3(dir.y, -dir.x, dir.z);
        Vector3 ccw = new Vector3(-dir.y, dir.x, dir.z);
        //Create the quad        
        AddQuad(start + cw, end + ccw, end + cw, start + ccw, verts, norms, tris, ref baseIndex);
    }

    public static void AddQuad(Vector3 zero, Vector3 one, Vector3 two, Vector3 three, List<Vector3> verts, List<Vector3> norms, List<int> tris, ref int baseIndex)
    {
        if (Warp)
        {
            zero = CylinderTransform(zero);
            one = CylinderTransform(one);
            two = CylinderTransform(two);
            three = CylinderTransform(three);
        }
        AddVertices(zero, one, two, three, verts, norms, tris, ref baseIndex);
    }

    public static Vector3 CylinderTransform(Vector3 p)
    {
        float radius = WarpRadius + p.z;
        return new Vector3(p.x*WarpFactor, radius * Mathf.Sin(p.y * Mathf.PI * 2.0f / WarpLength), radius * Mathf.Cos(p.y * Mathf.PI * 2.0f / WarpLength));
    }

    private static void AddVertices(Vector3 zero, Vector3 one, Vector3 two, Vector3 three, List<Vector3> verts, List<Vector3> norms, List<int> tris, ref int baseIndex)
    {
        //Lower left
        verts.Add(zero);
        norms.Add(Vector3.forward);
        //Upper right
        verts.Add(one);
        norms.Add(Vector3.forward);
        //Lower right
        verts.Add(two);
        norms.Add(Vector3.forward);
        //Upper left
        verts.Add(three);
        norms.Add(Vector3.forward);
        //Add the indices
        //Left triangle
        tris.Add(baseIndex + 0);
        tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 2);
        //Right triangle
        tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 0);
        tris.Add(baseIndex + 3);
        baseIndex += 4;
    }
}
