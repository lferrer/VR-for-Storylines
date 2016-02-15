using UnityEngine;
using RandomColorGenerator;
using System.Collections;
using System.Collections.Generic;
using System.IO;

//This script parses a layout TSV file and generates the geometry.
public class GeometryGenerator : MonoBehaviour
{

    //The TSV with the layout
    public string LayoutFile;

    //The path half width
    public float LineWidth = 0.25f;

    //The storyline size - width, height
    public Vector2 StoryLineSize = new Vector2(500.0f, 50.0f);

    public float WarpRadius = 50.0f;

    public float WarpFactor = 1.5f;

    //The line prefab
    public GameObject LinePrefab;

    //Number of vertices that will make the Bezier curves
    public int CurveSegments = 6;

    public Color[] LineColors;

    //True = Cylinder, False = Plane
    public bool Type = false;

    public float RotationSpeed = 0.5f;

    private float curveSteps;

    void Start()
    {
        var lines = File.ReadAllLines(LayoutFile);
        curveSteps = Mathf.Min(1.0f / CurveSegments, 0.25f);
        Vector2 scale = new Vector2(StoryLineSize.x / (lines[0].Split('\t').Length - 1), StoryLineSize.y / (lines.Length - 1));
        DataImporter.Warp = Type;
        DataImporter.WarpRadius = WarpRadius;
        DataImporter.WarpFactor = WarpFactor;
        DataImporter.WarpLength = WarpFactor*StoryLineSize.y;

        //The first line is used for headers
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Split('\t');
            var newLine = Instantiate(LinePrefab);
            newLine.name = line[0];
            KeyValuePair<Vector2, Vector2>[] positions = DataImporter.ParsePostions(line);            
            Vector3[] newVertices, newNormals;
            int[] newTriangles;
            GenerateGeometry(positions, scale, newLine, out newVertices, out newNormals, out newTriangles);            
            Mesh mesh = new Mesh();
            mesh.vertices = newVertices;
            mesh.triangles = newTriangles;
            mesh.normals = newNormals;
            newLine.GetComponent<MeshFilter>().mesh = mesh;
            newLine.transform.parent = transform;
            Color nc = LineColors[i - 1];
            nc.a = 255;
            newLine.GetComponent<Renderer>().material.SetColor("_Color", nc);
            foreach(var textMesh in newLine.transform.GetComponentsInChildren<TextMesh>())
                textMesh.color = nc;
        }
    }

    //Transforms the positions into vertices in the z = 0 plane
    void GenerateGeometry(KeyValuePair<Vector2, Vector2>[] positions, Vector2 scale, GameObject obj, out Vector3[] vertices, out Vector3[] normals, out int[] triangles)
    {
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<int> tris = new List<int>();
        Vector3 textOffset = Vector3.down * LineWidth / 2.0f;
        //Keeps track of the added vertices
        int baseIndex = 0;
        //Always reate a straight path for the first pair
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
        DataImporter.AddStraightPath(prevStart, prevEnd, LineWidth, verts, norms, tris, ref baseIndex);
        //Add the text at the beginning of the line
        var textGO = Instantiate(obj.transform.GetChild(0));
        var textMesh = textGO.transform.GetComponentInChildren<TextMesh>();
        textMesh.text = obj.name;
        Vector3 textPos = prevStart + textOffset;
        float xAngle = 360.0f * textPos.y / (WarpFactor * StoryLineSize.y);
        if (Type)
        {            
            if (Mathf.Abs(xAngle) > 90.0f)
            {
                textMesh.transform.Rotate(Vector3.right, 180.0f - xAngle);
                textMesh.transform.Rotate(Vector3.up, 180.0f);
                var renderer = textMesh.GetComponent<Renderer>();
                float textWidth = (renderer.bounds.max.x - renderer.bounds.min.x) / 2.0f;
                textPos -= 2.0f * textOffset + Vector3.left * textWidth;
            }
            else
                textMesh.transform.Rotate(Vector3.right, 360.0f - xAngle);
        }
        textMesh.transform.position = Type ? DataImporter.CylinderTransform(textPos) : textPos;
        textGO.transform.parent = obj.transform;
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
                DataImporter.AddBezierCurve(prevEnd, start, LineWidth, curveSteps, verts, norms, tris, ref baseIndex, clipSize);
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

            //Add the text at the beginning of the line only if it fits
            if (end.x - start.x > obj.name.Length * 1.5)
            {
                textGO = Instantiate(obj.transform.GetChild(0));
                textMesh = textGO.transform.GetComponentInChildren<TextMesh>();
                textMesh.text = obj.name;
                textPos = start + textOffset;
                if (Type)
                {
                    xAngle = 360.0f * textPos.y / (WarpFactor * StoryLineSize.y);
                    if (Mathf.Abs(xAngle) > 90.0f)
                    {
                        textMesh.transform.Rotate(Vector3.right, 180.0f - xAngle);
                        textMesh.transform.Rotate(Vector3.up, 180.0f);
                        var renderer = textMesh.GetComponent<Renderer>();
                        float textWidth = (renderer.bounds.max.x - renderer.bounds.min.x) / 2.0f;
                        textPos -= 2.0f * textOffset + Vector3.left * textWidth;
                    }
                    else
                        textMesh.transform.Rotate(Vector3.right, 360.0f - xAngle);
                }
                textMesh.transform.position = Type ? DataImporter.CylinderTransform(textPos) : textPos;
                textGO.transform.parent = obj.transform;
            }

            //Add rest of the path            
            DataImporter.AddStraightPath(start, end, LineWidth, verts, norms, tris, ref baseIndex);

        }
        vertices = verts.ToArray();
        triangles = tris.ToArray();
        normals = norms.ToArray();
    }

    private void FixedUpdate()
    {
        if (Type)
        {
            if (Input.GetMouseButton(0))
                transform.Rotate(Vector3.right, RotationSpeed, Space.Self);
            if (Input.GetMouseButton(1))
                transform.Rotate(Vector3.left, RotationSpeed, Space.Self);
        }

    }

}