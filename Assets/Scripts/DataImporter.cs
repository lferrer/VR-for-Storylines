using UnityEngine;
using RandomColorGenerator;
using System.Collections;
using System.Collections.Generic;
using System.IO;

//This script parses a layout TSV file and generates the geometry.
public class DataImporter : MonoBehaviour
{

    //The TSV with the layout
    public string LayoutFile;

    //The path half width
    public float LineWidth = 0.25f;

    //The storyline size - width, height
    public Vector2 StoryLineSize = new Vector2(100.0f, 10.0f);

    //The line prefab
    public GameObject LinePrefab;

    //The lines container for shared transformation
    public Transform LinesParent;    

    //Number of vertices that will make the Bezier curves
    public int CurveSegments = 6;

    private float curveSteps;

    void Start()
    {
        var lines = File.ReadAllLines(LayoutFile);
        curveSteps = Mathf.Min(1.0f / CurveSegments, 0.25f);
        Vector2 scale = new Vector2(StoryLineSize.x / (lines[0].Split('\t').Length - 1), StoryLineSize.y / (lines.Length - 1));
        //The first line is used for headers
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Split('\t');
            var newLine = Instantiate(LinePrefab);
            newLine.name = line[0];
            KeyValuePair<Vector2, Vector2>[] positions = ParsePostions(line);            
            Vector3[] newVertices, newNormals;
            int[] newTriangles;
            GenerateGeometry(positions, scale, out newVertices, out newNormals, out newTriangles);            
            Mesh mesh = new Mesh();
            mesh.vertices = newVertices;
            mesh.triangles = newTriangles;
            mesh.normals = newNormals;
            newLine.GetComponent<MeshFilter>().mesh = mesh;
            newLine.transform.parent = LinesParent;
            Color nc = RandomColor.GetColor(ColorScheme.Random, Luminosity.Dark);
            newLine.GetComponent<Renderer>().material.SetColor("_Color", nc);            
        }
    }

    //Encoding in vector2 for convenience. First dimension is time and second is height.
    KeyValuePair<Vector2, Vector2>[] ParsePostions(string[] line)
    {
        List<KeyValuePair<Vector2,Vector2>> res = new List<KeyValuePair<Vector2, Vector2>>();
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

    //Transforms the positions into vertices in the z = 0 plane
    void GenerateGeometry(KeyValuePair<Vector2, Vector2>[] positions, Vector2 scale, out Vector3[] vertices, out Vector3[] normals, out int[] triangles)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();        
        var cPos = positions[0];
        //Keeps track of the added vertices
        int baseIndex = 0;
        //Create a straight path for the first pair always
        KeyValuePair<Vector2, Vector2> pos = positions[0];
        Vector3 prevStart = new Vector3(pos.Key.x * scale.x, pos.Key.y * scale.y, 0);        
        Vector3 prevEnd = new Vector3(pos.Value.x * scale.x, pos.Value.y * scale.y, 0);        
        float clipSize = 0.0f;
        if (positions.Length > 1)
        {
            Vector3 nextStart = new Vector3(positions[1].Key.x * scale.x, positions[1].Key.y * scale.y, 0);
            Vector3 nextEnd = new Vector3(positions[1].Value.x * scale.x, positions[1].Value.y * scale.y, 0);
            if (nextStart.x - prevEnd.x <= scale.x)
            {
                clipSize = Mathf.Min(nextEnd.x - nextStart.x, prevEnd.x - prevStart.x, Mathf.Abs(nextStart.y - prevStart.y)) / 2.0f;                
                prevEnd.x -= clipSize;
            }
        }
        AddStraightPath(prevStart, prevEnd, verts, norms, tris, ref baseIndex);
        prevEnd.x += clipSize;
        for (int i = 1; i < positions.Length; i++)
        {
            pos = positions[i];
            Vector3 start = new Vector3(pos.Key.x * scale.x, pos.Key.y * scale.y, 0);
            Vector3 end = new Vector3(pos.Value.x * scale.x, pos.Value.y * scale.y, 0);
            //Do we have to add a curve before?
            if (start.x - prevEnd.x <= scale.x)
            {
                start.x += clipSize;
                prevEnd.x -= clipSize;
                AddBezierCurve(prevEnd, start, verts, norms, tris, ref baseIndex, clipSize);
            }
            //Store end before we modify it
            prevEnd = end;

            //Shorten the end if the next segment is contiguous
            if (i + 1 < positions.Length)
            {
                Vector3 nextStart = new Vector3(positions[i + 1].Key.x * scale.x, positions[i + 1].Key.y * scale.y, 0);
                Vector3 nextEnd = new Vector3(positions[i + 1].Value.x * scale.x, positions[i + 1].Value.y * scale.y, 0);
                if (nextStart.x - end.x <= scale.x)
                {                    
                    clipSize = Mathf.Min(nextEnd.x - nextStart.x, end.x - start.x, Mathf.Abs(nextStart.y - start.y)) / 2.0f;                    
                    end.x -= clipSize;
                }
            }

            //Add rest of the path            
            AddStraightPath(start, end, verts, norms, tris, ref baseIndex);            
            
        }
        vertices = verts.ToArray();
        triangles = tris.ToArray();
        normals = norms.ToArray();
    }

    //Ads a bezier curved path between two middle points
    void AddBezierCurve(Vector3 start, Vector3 end, List<Vector3> verts, List<Vector3> norms, List<int> tris, ref int baseIndex, float curveOffset)
    {
        //Make the curves pop out
        start.z -= 0.1f;
        end.z -= 0.1f;

        //Calculate the two starting points
        Vector3 dir = LineWidth * Vector3.right;
        Vector3 cw = new Vector3(dir.y, -dir.x, dir.z);
        Vector3 ccw = new Vector3(-dir.y, dir.x, dir.z);
        //Lower left
        Vector3 lowPrev = start + cw;
        Vector3 highPrev = start + ccw;

        //Calculate the Bezier offset points
        Vector3 startOffset = start + Vector3.right * curveOffset;
        Vector3 endOffset = end + Vector3.left * curveOffset;        

        float t = curveSteps;
        Vector3 prev = start;
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
            tangent *= LineWidth;
            cw = new Vector3(tangent.y, -tangent.x, tangent.z);
            ccw = new Vector3(-tangent.y, tangent.x, tangent.z);            
            Vector3 lowNext = next + cw;
            Vector3 highNext = next + ccw;

            AddQuad(lowPrev, highNext, lowNext, highPrev, verts, norms, tris, ref baseIndex);
            t += curveSteps;
            lowPrev = lowNext;
            highPrev = highNext;            
        }

        //Make the curves pop out
        start.z += 0.1f;
        end.z += 0.1f;
    }

    //Ads a rectangular segment between two middle points
    void AddStraightPath(Vector3 start, Vector3 end, List<Vector3> verts, List<Vector3> norms, List<int> tris, ref int baseIndex)
    {
        //Aux vectors
        Vector3 dir = end - start;
        dir.Normalize();
        dir *= LineWidth;
        Vector3 cw = new Vector3(dir.y, -dir.x, dir.z);
        Vector3 ccw = new Vector3(-dir.y, dir.x, dir.z);
        //Create the quad        
        AddQuad(start + cw, end + ccw, end + cw, start + ccw, verts, norms, tris, ref baseIndex);
    }

    void AddQuad(Vector3 zero, Vector3 one, Vector3 two, Vector3 three, List<Vector3> verts, List<Vector3> norms, List<int> tris, ref int baseIndex)
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