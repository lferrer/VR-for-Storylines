using UnityEngine;
using System.Collections.Generic;

public class DataImporter {

    public enum LineType { Tape, Cylinder, Wedge };

    public static bool Warp { get; set; }
    public static float WarpRadius { get; set; }
    public static float WarpLength { get; set; }
    public static float WarpFactor { get; set; }
    public static LineType Type = LineType.Tape;

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

    private struct Bundle
    {
        public float Start, End;
        public List<int> Children;
    }

    public static int[] BundleLines(string[] lines, int numBins)
    {
        float[] scores = new float[lines.Length];
        int[] bins = new int[lines.Length];
        List<List<Bundle>> bundles = new List<List<Bundle>>();
        var testLine = lines[0].Split('\t');
        for (int j = 0; j < testLine.Length; j++)
            bundles.Add(new List<Bundle>());

        //The first line is used for headers
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Split('\t');
            //The first column is used for the name
            for (int j = 1; j < line.Length; j++)
            {
                float height;
                if (float.TryParse(line[j], out height))
                {
                    bool inBundle = false;
                    for (int k = 0; k < bundles[j].Count; k++)
                    {
                        Bundle bun = bundles[j][k];
                        if (bun.Start - 1 <= height && bun.End + 1 >= height)
                        {
                            bun.Start = Mathf.Min(bun.Start, height);
                            bun.End = Mathf.Max(bun.End, height);
                            bun.Children.Add(i);
                            inBundle = true;
                        }
                    }
                    if (!inBundle)
                    {
                        Bundle b = new Bundle();
                        b.Start = height;
                        b.End = height;
                        b.Children = new List<int>();
                        b.Children.Add(i);
                        bundles[j].Add(b);
                    }
                }
            }
        }

        foreach (List<Bundle> lb in bundles)
        {
            foreach (Bundle bun in lb)
            {
                foreach (int line in bun.Children)
                {
                    scores[line] += bun.Children.Count;
                }
            }
        }
        float[] intervals = MakeIntervals(scores, numBins);
        for (int i = 0; i < lines.Length; i++)
        {
            int bin = Bin(scores[i], intervals);
            bins[i] = bin;
        }
        return bins;
    }

    static float[] MakeIntervals(float[] data, int numBins)
    {
        float max = data[0]; // find min & max
        float min = data[0];
        for (int i = 0; i < data.Length; ++i)
        {
            if (data[i] < min) min = data[i];
            if (data[i] > max) max = data[i];
        }
        float width = (max - min) / numBins; // compute width

        float[] intervals = new float[numBins * 2]; // intervals
        intervals[0] = min;
        intervals[1] = min + width;
        for (int i = 2; i < intervals.Length - 1; i += 2)
        {
            intervals[i] = intervals[i - 1];
            intervals[i + 1] = intervals[i] + width;
        }
        intervals[0] = float.MinValue; // outliers
        intervals[intervals.Length - 1] = float.MaxValue;

        return intervals;
    }

    static int Bin(float x, float[] intervals)
    {
        for (int i = 0; i < intervals.Length - 1; i += 2)
        {
            if (x >= intervals[i] && x < intervals[i + 1])
                return i / 2;
        }
        return -1; // error
    }


    //Adds a bezier curved path between two middle points
    public static void AddBezierCurve(Vector3 start, Vector3 end, float lineWidth, float curveSteps, List<Vector3> verts, List<int> tris, ref int baseIndex, float curveOffset)
    {
        Vector3 prev = start;
        //Calculate the Bezier offset points
        Vector3 startOffset = start + Vector3.right * curveOffset;
        Vector3 endOffset = end + Vector3.left * curveOffset;
        Vector3 nStart = Vector3.right;
        float t = curveSteps;        
        while (t <= 1)
        {
            Vector3 next = Mathf.Pow(1.0f - t, 3) * start +
                          3.0f * Mathf.Pow(1.0f - t, 2) * t * startOffset +
                          3.0f * (1.0f - t) * t * t * endOffset +
                          Mathf.Pow(t, 3) * end;
            Vector3 nEnd = 3.0f * Mathf.Pow(1.0f - t, 2) * (startOffset - start) +
                              6.0f * (1.0f - t) * t * (endOffset - startOffset) +
                              3.0f * t * t * (end - endOffset);            
            t += curveSteps;
            nEnd.Normalize();
            AddStraightPath(lineWidth, prev, next, Vector3.forward, Vector3.forward, nStart, nEnd, verts, tris, ref baseIndex);            
            prev = next;
            nStart = nEnd;
        }
    }

    public static void AddStraightPath(float lineWidth, Vector3 start, Vector3 end, List<Vector3> verts, List<int> tris, ref int baseIndex)
    {
        Vector3 n = end - start;
        n.Normalize();
        Vector3 u = new Vector3(n.y, -n.x, n.z);
        AddStraightPath(lineWidth, start, end, u, u, n, n, verts, tris, ref baseIndex);
    }

    public static void AddStraightPath(float lineWidth, Vector3 start, Vector3 end, Vector3 uStart, Vector3 uEnd, 
        Vector3 nStart, Vector3 nEnd, List<Vector3> verts, List<int> tris, ref int baseIndex)
    {
        if (Warp)
        {
            start = CylinderTransform(start);
            end = CylinderTransform(end);
        }
        switch (Type)
        {
            case LineType.Tape:                
                Vector3 dir = end - start;
                dir.Normalize();
                dir *= lineWidth;
                Vector3 cw = new Vector3(dir.y, -dir.x, dir.z);
                Vector3 ccw = new Vector3(-dir.y, dir.x, dir.z);
                AddQuad(start + cw, end + ccw, end + cw, start + ccw, verts, tris, ref baseIndex);
                break;
            case LineType.Wedge:
            case LineType.Cylinder:
                //Diamater of the cylinder times the proportional part that the line is of it
                var radius = 2.0f * Mathf.PI * WarpRadius * 2.0f * lineWidth / WarpLength;
                var lowEnd = radius * uEnd + Vector3.Cross(Vector3.zero, uEnd) + end;
                var lowStart = radius * uStart + Vector3.Cross(Vector3.zero, uStart) + start;
                var cylinderStep = Type == LineType.Cylinder ? Mathf.PI * 2.0f / 16.0f : Mathf.PI * 2.0f / 4.0f;
                float t = cylinderStep;
                while (t <= Mathf.PI * 2.0f + cylinderStep)
                {
                    var upEnd = radius * Mathf.Cos(t) * uEnd + Vector3.Cross(radius * Mathf.Sin(t) * nEnd, uEnd) + end;
                    var upStart = radius * Mathf.Cos(t) * uStart + Vector3.Cross(radius * Mathf.Sin(t) * nStart, uStart) + start;
                    AddQuad(lowStart, upEnd, lowEnd, upStart, verts, tris, ref baseIndex);
                    lowEnd = upEnd;
                    lowStart = upStart;
                    t += cylinderStep;
                }
                break;
        }
        
    }

    public static Vector3 CylinderTransform(Vector3 p, float offset)
    {
        float radius = WarpRadius + p.z;
        offset *= Mathf.Deg2Rad;
        return new Vector3(p.x * WarpFactor, radius * Mathf.Sin(offset + p.y * Mathf.PI * 2.0f / WarpLength), radius * Mathf.Cos(offset + p.y * Mathf.PI * 2.0f / WarpLength));
    }

    public static Vector3 CylinderTransform(Vector3 p)
    {
        return CylinderTransform(p, 0);
    }

    public static void AddQuad(Vector3 zero, Vector3 one, Vector3 two, Vector3 three, List<Vector3> verts, List<int> tris, ref int baseIndex)
    {        
        //Lower left
        verts.Add(zero);
        //Upper right
        verts.Add(one);
        //Lower right
        verts.Add(two);
        //Upper left
        verts.Add(three);
        //Add the indices
        //Right triangle
        tris.Add(baseIndex + 0);
        tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 2);
        //Left triangle
        tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 0);
        tris.Add(baseIndex + 3);
        baseIndex += 4;
    }
}
