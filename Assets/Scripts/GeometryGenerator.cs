using UnityEngine;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

//This script parses a layout TSV file and generates the geometry.
public class GeometryGenerator : MonoBehaviour
{
    //The TSV with the layout
    public string[] LayoutFiles;

    //The path half width
    public float[] LineWidths;
    public int[] LabelFontSizes;

    //The storyline size - width, height
    public Vector2[] StoryLineSizes;

    public float[] WarpRadiuses;
    public float[] WarpFactors;

    public float[] TickScales;
    public int[] TickFontSizes;

    public float[] LayerDepth;
    public float[] LineSeparation;

    public float WarpChangeSpeed;

    //The line prefab
    public GameObject LinePrefab, TickPrefab;

    //Number of vertices that will make the Bezier curves
    public int CurveSegments = 6;
    public int NumTicks = 10;


    public Color[] LineColors;

    //True = Cylinder, False = Plane
    public bool Type = false;
    public DataImporter.LineType LineType = DataImporter.LineType.Cylinder;
    public Material[] Materials;

    public float RotationSpeed = 0.5f;
    public int NumBins = 3;

    private float curveSteps;
    private Dictionary<TextMesh, float> textRotations;
    private Dictionary<TextMesh, Vector3> textPositions;
    private List<TextMesh> keys;
    private Vector3 textOffset;
    private float[] bWarpRadiuses;

    public int StoryIndex { get; set; }
    public float cRotation { get; set; }
    public int BinOffset { get; set; }

    void Start()
    {        
        BinOffset = 0;
        cRotation = 0;
        StoryIndex = 1;
        Stopwatch st = new Stopwatch();
        st.Start();
        Draw();
        st.Stop();
        UnityEngine.Debug.Log(string.Format("Geometry generation took {0} ms to complete", st.ElapsedMilliseconds));
    }

    void Draw()
    {
        var lines = File.ReadAllLines(LayoutFiles[StoryIndex]);
        curveSteps = Mathf.Min(1.0f / CurveSegments, 0.25f);
        Vector2 scale = new Vector2(StoryLineSizes[StoryIndex].x / (lines[0].Split('\t').Length - 1), StoryLineSizes[StoryIndex].y / (lines.Length - 1));
        DataImporter.Warp = Type;
        DataImporter.WarpRadius = WarpRadiuses[StoryIndex];
        DataImporter.WarpFactor = WarpFactors[StoryIndex];
        DataImporter.WarpLength = WarpFactors[StoryIndex] * StoryLineSizes[StoryIndex].y;
        DataImporter.Type = LineType;
        textRotations = new Dictionary<TextMesh, float>();
        textPositions = new Dictionary<TextMesh, Vector3>();
        keys = new List<TextMesh>();
        textOffset = Vector3.down * LineWidths[StoryIndex];
        int[] bins = DataImporter.BundleLines(lines, NumBins);

        //The first line is used for headers
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Split('\t');
            var newLine = Instantiate(LinePrefab);
            newLine.name = line[0];
            newLine.transform.position = Vector3.zero;
            KeyValuePair<Vector2, Vector2>[] positions = DataImporter.ParsePostions(line);
            int bin = ClampAround(bins[i] + BinOffset, 0, NumBins - 1);
            Vector3[] newVertices;
            int[] newTriangles;
            int layer = bin == NumBins - 1 - BinOffset ? 8 : 0;
            GenerateGeometry(positions, scale, newLine, layer, out newVertices, out newTriangles);
            Mesh mesh = new Mesh();
            mesh.vertices = newVertices;
            mesh.triangles = newTriangles;
            mesh.RecalculateNormals();
            newLine.GetComponent<MeshFilter>().mesh = mesh;
            newLine.transform.parent = transform;
            Color c = LineColors[(i - 1)% LineColors.Length];
            HSBColor nColor = HSBColor.FromColor(c);            
            var factor = bin / (NumBins - 1);
            nColor.b *= 0.75f * factor + 0.25f;
            Color nc = nColor.ToColor();
            var renderer = newLine.GetComponent<Renderer>();
            renderer.material = Materials[(int)LineType];
            renderer.material.SetColor("_Color", nc);
            renderer.material.SetColor("_EmissionColor", nc);
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.EnableKeyword("_METALLICGLOSSMAP");
            renderer.material.EnableKeyword("_SPECGLOSSMAP");
            nc.a = 255.0f;
            foreach (var tm in newLine.transform.GetComponentsInChildren<TextMesh>())
                tm.color = nc;

            //Add text for the close up
            GameObject textGO = Instantiate(newLine.transform.GetChild(0).gameObject);
            textGO.name = "CloseUp";
            var textMesh = textGO.GetComponentInChildren<TextMesh>();
            var pos = positions[0];
            Vector3 start = new Vector3(pos.Key.x * scale.x, pos.Key.y * scale.y, 0.0f);
            pos = positions[positions.Length - 1];
            Vector3 end = new Vector3(pos.Value.x * scale.x, pos.Value.y * scale.y, 0.0f);
            int n = (int)((end.x - start.x) / 1500);
            n = Mathf.Clamp(n, 1, 4);
            string[] words = TextGenerator.TopNWords(n, i);
            var text = words[0];
            for (int k = 1; k < n; k++)
                text += "," + words[k];
            textMesh.text = text;
            var textPos = (end - start) / 2.0f + textOffset;
            if (Type)
            {
                float xAngle = 360.0f * textPos.y / (WarpFactors[StoryIndex] * StoryLineSizes[StoryIndex].y);
                textRotations.Add(textMesh, xAngle + 180.0f);
                textPositions.Add(textMesh, textPos);
                keys.Add(textMesh);
            }
            textMesh.transform.position = Type ? DataImporter.CylinderTransform(textPos) : textPos;
            textMesh.fontSize = Type ? LabelFontSizes[StoryIndex] : LabelFontSizes[StoryIndex] / 4;
            textGO.transform.parent = newLine.transform;
            textGO.SetActive(false);            
            float depth = (1.0f - LayerDepth[StoryIndex]) + LayerDepth[StoryIndex] * (NumBins - bin) / NumBins;
            depth -= LineSeparation[StoryIndex] * i / NumBins;
            Vector3 objScale = new Vector3(1.0f, depth, depth);
            newLine.transform.localScale = objScale;
        }
        var ticks = GameObject.FindGameObjectsWithTag("Tick");
        foreach (var t in ticks)
            Destroy(t);
        if (Type)
        { 
            float tickSpace = WarpFactors[StoryIndex] * StoryLineSizes[StoryIndex].x / NumTicks;
            var tickRadius = WarpRadiuses[StoryIndex] + LayerDepth[StoryIndex] + 2.0f * Mathf.PI * LineWidths[StoryIndex] + LineSeparation[StoryIndex];
            int baseIndex = 0;
            List<Vector3> newVertices = new List<Vector3>();
            List<int> newTriangles = new List<int>();
            Vector3 prevSmall = new Vector3(tickRadius, 0, 0);
            Vector3 prevBig = new Vector3(tickRadius * 1.25f, 0, 0);
            for (int j = 1; j < 360; j++)
            {
                float angle = 2.0f * Mathf.PI * j / 359.0f;
                var nextSmall = tickRadius * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var nextBig = tickRadius * 1.25f * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                DataImporter.AddQuad(prevSmall, nextBig, nextSmall, prevBig, newVertices, newTriangles, ref baseIndex);
                prevBig = nextBig;
                prevSmall = nextSmall;
            }
            Mesh mesh = new Mesh();
            mesh.vertices = newVertices.ToArray();
            mesh.triangles = newTriangles.ToArray();
            for (int i = 0; i <= NumTicks; i++)
            {
                var newTick = Instantiate(TickPrefab);
                newTick.transform.position = new Vector3(i * tickSpace, 0, 0);
                newTick.tag = "Tick";
                var lineGO = newTick.transform.GetChild(0).gameObject;                
                lineGO.GetComponent<MeshFilter>().mesh = mesh;
                var textGO = newTick.transform.GetChild(1).gameObject;
                var textMesh = textGO.GetComponent<TextMesh>();
                textMesh.text = "t = " + (i + 1);
                textMesh.text = "";
                textMesh.fontSize = Type ? TickFontSizes[StoryIndex] : TickFontSizes[StoryIndex] / 4;
                textGO.transform.position = new Vector3(i * tickSpace, 0, tickRadius);
                textGO.transform.rotation = Quaternion.Euler(new Vector3(0, 90, 0));
                var textGO2 = newTick.transform.GetChild(2).gameObject;
                var textMesh2 = textGO2.GetComponent<TextMesh>();
                textMesh2.text = "t = " + (i + 1);
                textMesh2.fontSize = Type ? TickFontSizes[StoryIndex] : TickFontSizes[StoryIndex] / 4;
                textGO2.transform.position = new Vector3(i * tickSpace, 0, -tickRadius);
                textGO2.transform.rotation = Quaternion.Euler(new Vector3(0, 270, 0));
            }
        }
        FlipLabels(0);
    }

    //Transforms the positions into vertices in the z = 0 plane
    private void GenerateGeometry(KeyValuePair<Vector2, Vector2>[] positions, Vector2 geometryScale, GameObject obj, int layer, out Vector3[] vertices, out int[] triangles)
    {
        List<Vector3> verts = new List<Vector3>();        
        List<int> tris = new List<int>();        
        //Keeps track of the added vertices
        int baseIndex = 0;
        //Always create a straight path for the first pair
        KeyValuePair<Vector2, Vector2> pos = positions[0];
        Vector3 prevStart = new Vector3(pos.Key.x * geometryScale.x, pos.Key.y * geometryScale.y, 0.0f);
        Vector3 prevEnd = new Vector3(pos.Value.x * geometryScale.x, pos.Value.y * geometryScale.y, 0.0f);
        float clipSize = 0.0f;
        if (positions.Length > 1)
        {
            Vector3 nextStart = new Vector3(positions[1].Key.x * geometryScale.x, positions[1].Key.y * geometryScale.y, 0.0f);
            Vector3 nextEnd = new Vector3(positions[1].Value.x * geometryScale.x, positions[1].Value.y * geometryScale.y, 0.0f);
            if (nextStart.x - prevEnd.x <= geometryScale.x)
            {
                clipSize = Mathf.Min(nextEnd.x - nextStart.x, prevEnd.x - prevStart.x, Mathf.Abs(nextStart.y - prevStart.y)) / 2.0f;
                prevEnd.x -= clipSize;
            }
        }
        DataImporter.AddStraightPath(LineWidths[StoryIndex], prevStart, prevEnd, verts, tris, ref baseIndex);
        AddCollider(LineWidths[StoryIndex], prevStart, prevEnd, obj, layer);
        //Add the text at the beginning of the line
        var textGO = Instantiate(obj.transform.GetChild(0));
        var textMesh = textGO.transform.GetComponentInChildren<TextMesh>();
        textMesh.text = obj.name;
        Vector3 textPos = prevStart + textOffset;        
        if (Type)
        {
            float xAngle = 360.0f * textPos.y / (WarpFactors[StoryIndex] * StoryLineSizes[StoryIndex].y);
            textRotations.Add(textMesh, xAngle + 180.0f);
            textPositions.Add(textMesh, textPos);
            keys.Add(textMesh);
        }        
        textMesh.transform.position = Type ? DataImporter.CylinderTransform(textPos) : textPos;
        textMesh.fontSize = Type ? LabelFontSizes[StoryIndex] : LabelFontSizes[StoryIndex] / 4;        
        textGO.transform.parent = obj.transform;
        prevEnd.x += clipSize;
        for (int i = 1; i < positions.Length; i++)
        {
            pos = positions[i];
            Vector3 start = new Vector3(pos.Key.x * geometryScale.x, pos.Key.y * geometryScale.y, 0.0f);
            Vector3 end = new Vector3(pos.Value.x * geometryScale.x, pos.Value.y * geometryScale.y, 0.0f);
            //Do we have to add a curve before?
            if (start.x - prevEnd.x <= geometryScale.x)
            {
                start.x += clipSize;
                prevEnd.x -= clipSize;
                DataImporter.AddBezierCurve(prevEnd, start, LineWidths[StoryIndex], curveSteps, verts, tris, ref baseIndex, clipSize);
            }
            //Store end before we modify it
            prevEnd = end;

            //Shorten the end if the next segment is contiguous
            if (i + 1 < positions.Length)
            {
                Vector3 nextStart = new Vector3(positions[i + 1].Key.x * geometryScale.x, positions[i + 1].Key.y * geometryScale.y, 0.0f);
                Vector3 nextEnd = new Vector3(positions[i + 1].Value.x * geometryScale.x, positions[i + 1].Value.y * geometryScale.y, 0.0f);
                if (nextStart.x - end.x <= geometryScale.x)
                {
                    clipSize = Mathf.Min(nextEnd.x - nextStart.x, end.x - start.x, Mathf.Abs(nextStart.y - start.y)) / 2.0f;
                    end.x -= clipSize;
                }
            }

            //Add the text at the beginning of the line only if it fits
            if (end.x - start.x > obj.name.Length * 2)
            {
                textGO = Instantiate(obj.transform.GetChild(0));
                textMesh = textGO.transform.GetComponentInChildren<TextMesh>();
                if (positions[i].Key.x - positions[i].Value.x <= 1)
                    textMesh.text = obj.name;
                textPos = start + textOffset;
                if (Type)
                {
                    float xAngle = 360.0f * textPos.y / (WarpFactors[StoryIndex] * StoryLineSizes[StoryIndex].y);
                    textRotations.Add(textMesh, xAngle + 180.0f);
                    textPositions.Add(textMesh, textPos);
                    keys.Add(textMesh);
                }
                textMesh.transform.position = Type ? DataImporter.CylinderTransform(textPos) : textPos;
                textMesh.fontSize = Type ? LabelFontSizes[StoryIndex] : LabelFontSizes[StoryIndex] / 4;
                textGO.transform.parent = obj.transform;
            }            

            //Add rest of the path            
            DataImporter.AddStraightPath(LineWidths[StoryIndex], start, end, verts, tris, ref baseIndex);
            AddCollider(LineWidths[StoryIndex], start, end, obj, layer);
        }
        vertices = verts.ToArray();
        triangles = tris.ToArray();
    }

    private void AddCollider(float lineWidth, Vector3 start, Vector3 end, GameObject parent, int layer)
    {
        start = Type ? DataImporter.CylinderTransform(start) : start;
        end = Type ? DataImporter.CylinderTransform(end) : end;
        GameObject child = new GameObject();
        child.layer = layer;
        CapsuleCollider collider = child.AddComponent<CapsuleCollider>();
        collider.direction = 0; // X-axis
        collider.center = (start + end) / 2.0f;        
        collider.radius = 2.0f * Mathf.PI * DataImporter.WarpRadius * 8.0f * lineWidth / DataImporter.WarpLength;
        collider.height = (end - start).magnitude;
        child.transform.parent = parent.transform;
    }

    public void ChangeWarp(float amount)
    {
        WarpRadiuses[StoryIndex] += amount;
        Reset();
    }

    public void Reset()
    {
        foreach (Transform child in transform)        
            Destroy(child.gameObject);
        Draw();        
    }

    public void ResetView()
    {
        transform.rotation = Quaternion.identity;
        cRotation = 0;
        FlipLabels(0);        
    }

    public void Rotate(bool dir)
    {
        if (Type)
        {
            if (dir)
            {
                transform.Rotate(Vector3.right, RotationSpeed, Space.Self);
                FlipLabels(-RotationSpeed);
            }
            else
            {
                transform.Rotate(Vector3.left, RotationSpeed, Space.Self);
                FlipLabels(RotationSpeed);
            }
        }
    }

    private float ClampAround(float value, float min, float max)
    {
        while(value > max)
        {
            value -= max;
        }
        while(value < min)
        {
            value += max;
        }
        return value;
    }

    private int ClampAround(int value, int min, int max)
    {
        while (value > max)
        {
            value -= max;
        }
        while (value < min)
        {
            value += max;
        }
        return value;
    }

    private void FlipLabels(float speed)
    {
        cRotation = ClampAround(cRotation + speed, 0, 360);
        foreach (var textMesh in keys)
        {
            float xAngle = ClampAround(textRotations[textMesh] + cRotation, 0, 360);
            textMesh.transform.rotation = Quaternion.identity;
            var textPos = textPositions[textMesh];
            if (xAngle < 90 || xAngle > 270)
            {
                textMesh.transform.Rotate(Vector3.right, 360.0f - xAngle);
                textMesh.transform.Rotate(Vector3.up, 180.0f);
                var renderer = textMesh.GetComponent<Renderer>();
                float textWidth = (renderer.bounds.max.x - renderer.bounds.min.x) / 2.0f;
                textPos -= 2.0f * textOffset + Vector3.left * textWidth;                
            }
            else
            {
                textMesh.transform.Rotate(Vector3.right, 180.0f - xAngle);
            }
                
            textMesh.transform.position = DataImporter.CylinderTransform(textPos, cRotation);            
        }
    }

}