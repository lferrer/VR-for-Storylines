using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;
using UnityStandardAssets.Characters.FirstPerson;
using System.Collections.Generic;

public class InputController : MonoBehaviour
{
    public float MoveSpeed;
    public float ZoomSpeed;
    public GameObject LinesContainer;

    [SerializeField] private MouseLook m_MouseLook;
    private Camera m_Camera;
    private GeometryGenerator m_GeometryGenerator;
    private float endX, cylinderRadius;
    private GameObject character;
    private List<Vector3> startScales = new List<Vector3>();
    private List<Vector3> endScales = new List<Vector3>();
    private List<Vector3> originalScales = new List<Vector3>();
    private List<GameObject> closeUps = new List<GameObject>();
    private List<GameObject> openUps = new List<GameObject>();
    private bool takeHiResShot = false;


    private void Start()
    {
        m_Camera = Camera.main;
        character = transform.GetChild(0).gameObject;
        m_MouseLook.Init(transform, transform);
        m_GeometryGenerator = LinesContainer.GetComponent<GeometryGenerator>();
        endX = m_GeometryGenerator.WarpFactors[1] * m_GeometryGenerator.StoryLineSizes[1].x * 0.95f;
        cylinderRadius = m_GeometryGenerator.WarpRadiuses[m_GeometryGenerator.StoryIndex] * 0.7f;
        MoveSpeed *= endX;
    }

    private void Update()
    {
        m_MouseLook.LookRotation(transform, character.transform);

        // Read input
        float horizontal = CrossPlatformInputManager.GetAxis("Horizontal");
        float vertical = CrossPlatformInputManager.GetAxis("Vertical");        
        var m_Input = new Vector2(horizontal, vertical);

        // normalize input if it exceeds 1 in combined length:
        if (m_Input.sqrMagnitude > 1)
        {
            m_Input.Normalize();
        }
        m_Input *= MoveSpeed;

        //Rotate the whole cylinder
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1))
        {
            m_GeometryGenerator.Rotate(Input.GetMouseButton(0));
        }

        //Move along the X axis
        Vector3 desiredMove = m_Camera.transform.forward * m_Input.y + m_Camera.transform.right * m_Input.x;
        desiredMove.Scale(new Vector3(1, 0, 0));
        transform.position += desiredMove * Time.fixedDeltaTime;
        var newX = Mathf.Clamp(transform.position.x, 0, endX);
        transform.position = new Vector3(newX, 0, 0);
        bool resetPosition = false;

        //Reset view
        if (Input.GetKeyUp(KeyCode.PageDown) /*|| Input.GetKeyUp(KeyCode.PageUp)*/ || Input.GetKeyUp(KeyCode.Tab) || Input.GetKeyUp(KeyCode.Escape))
        {
            transform.rotation = Quaternion.identity;
            transform.position = Vector3.zero;
            m_Camera.transform.rotation = Quaternion.identity;
            m_Camera.transform.position = Vector3.zero;
            character.transform.rotation = Quaternion.identity;
            character.transform.position = Vector3.zero;            
            resetPosition = true;
            startScales.Clear();
            endScales.Clear();
            originalScales.Clear();
            closeUps.Clear();
            openUps.Clear();
            m_GeometryGenerator.ResetView();            
        }

        //Change between layouts
        /*if (Input.GetKeyUp(KeyCode.PageUp))
        {
            m_GeometryGenerator.Type = !m_GeometryGenerator.Type;            
            m_GeometryGenerator.Reset();
        }  */      

        //Change datasets
        if (Input.GetKeyUp(KeyCode.Tab))
        {
            /*m_GeometryGenerator.StoryIndex = m_GeometryGenerator.StoryIndex + 1 == m_GeometryGenerator.StoryLineSizes.Length ? 0 : m_GeometryGenerator.StoryIndex + 1;
            endX = m_GeometryGenerator.WarpFactors[m_GeometryGenerator.StoryIndex] * m_GeometryGenerator.StoryLineSizes[m_GeometryGenerator.StoryIndex].x;
            cylinderRadius = m_GeometryGenerator.WarpRadiuses[m_GeometryGenerator.StoryIndex] * 0.7f;*/
            m_GeometryGenerator.LineType = m_GeometryGenerator.LineType == DataImporter.LineType.Wedge ? DataImporter.LineType.Tape :
                (DataImporter.LineType)((int)m_GeometryGenerator.LineType + 1);            
            m_GeometryGenerator.Reset();                   
            
        }

        //Change line type
        if (Input.GetKeyUp(KeyCode.Escape))
        {
            /*m_GeometryGenerator.LineType = m_GeometryGenerator.LineType == DataImporter.LineType.Wedge ? DataImporter.LineType.Tape :
                (DataImporter.LineType)((int)m_GeometryGenerator.LineType + 1);            
            m_GeometryGenerator.Reset(); */
            m_GeometryGenerator.BinOffset++;
            if (m_GeometryGenerator.BinOffset > m_GeometryGenerator.NumBins - 1)
                m_GeometryGenerator.BinOffset = 0;
            m_GeometryGenerator.Reset();
        }

        //Change warp size
        /*if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.LeftAlt))
        {
            float warpSpeed = Input.GetKey(KeyCode.LeftAlt) ? m_GeometryGenerator.WarpChangeSpeed : -m_GeometryGenerator.WarpChangeSpeed;
            m_GeometryGenerator.ChangeWarp(warpSpeed);
            m_GeometryGenerator.cRotation = 0;
            cylinderRadius = m_GeometryGenerator.WarpRadiuses[m_GeometryGenerator.StoryIndex] * 0.7f;
            character.transform.position = new Vector3(character.transform.position.x, 
                Mathf.Clamp(character.transform.position.y, -cylinderRadius, cylinderRadius),
                Mathf.Clamp(character.transform.position.z, -cylinderRadius, cylinderRadius));
            startScales.Clear();
            endScales.Clear();
            originalScales.Clear();
            closeUps.Clear();
            openUps.Clear();
        }*/
       
        //Zoom into the cylinder
        if (Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space))
        {
            Vector3 offset = Input.GetKey(KeyCode.Space) ? -m_Camera.transform.forward : m_Camera.transform.forward;            
            character.transform.position += offset * ZoomSpeed;
            character.transform.position = new Vector3(
                Mathf.Clamp(character.transform.position.x, 0, endX),
                Mathf.Clamp(character.transform.position.y, -cylinderRadius, cylinderRadius),
                Mathf.Clamp(character.transform.position.z, -cylinderRadius, cylinderRadius));
        }

        if (resetPosition)
        {
            transform.position = m_GeometryGenerator.Type ? Vector3.zero : 
                new Vector3(0, 0, -m_GeometryGenerator.StoryLineSizes[m_GeometryGenerator.StoryIndex].y);
        }

        if (Input.GetKeyUp(KeyCode.P))
            takeHiResShot = true;

        //Check gaze position
        List<RaycastHit> hits = new List<RaycastHit>(Physics.RaycastAll(new Ray(m_Camera.transform.position, m_Camera.transform.forward), float.PositiveInfinity, 1 << 8));
        List<GameObject> newHits = new List<GameObject>();
        foreach (var hit in hits)
        {
            GameObject go = hit.collider.gameObject.transform.parent.gameObject;            
            newHits.Add(go);
        }
        for (int i = 0; i < closeUps.Count; i++)
        {
            if (!newHits.Contains(closeUps[i]))
            {
                GameObject go = closeUps[i];
                var textGO = go.transform.GetChild(go.transform.childCount - 1);
                textGO.gameObject.SetActive(false);
                originalScales.Add(startScales[i]);
                openUps.Add(closeUps[i]);                
                closeUps.RemoveAt(i);
                startScales.RemoveAt(i);
                endScales.RemoveAt(i);
                i--;
            }
        }
        
        foreach (var hit in newHits)
        {
            if (!closeUps.Contains(hit))
            {
                Vector3 startScale = hit.transform.localScale;
                Vector3 endScale = new Vector3(1.0f, startScale.y * 0.5f, startScale.z * 0.5f);
                startScales.Add(startScale);
                endScales.Add(endScale);
                closeUps.Add(hit);                
            }
        }

        for (int i = 0; i < closeUps.Count; i++)
        {
            GameObject go = closeUps[i];
            if (go.transform.localScale.sqrMagnitude > endScales[i].sqrMagnitude)
            {
                Vector3 speed = (Vector3.up + Vector3.forward) * Time.deltaTime * 0.5f;
                go.transform.localScale = go.transform.localScale - speed;
            }
            else
            {
                go.transform.localScale = endScales[i];
                var textGO = go.transform.GetChild(go.transform.childCount - 1);
                Vector3 pos = textGO.transform.position;
                pos.x = m_Camera.transform.position.x;
                textGO.transform.position = pos;
                textGO.gameObject.SetActive(true);
            }
        }

        for (int i = 0; i < openUps.Count; i++)
        {
            GameObject go = openUps[i];
            if (go.transform.localScale.sqrMagnitude < originalScales[i].sqrMagnitude)
            {
                Vector3 speed = (Vector3.up + Vector3.forward) * Time.deltaTime * 0.5f;
                go.transform.localScale = go.transform.localScale + speed;
            }
            else
            {
                go.transform.localScale = originalScales[i];               
                openUps.RemoveAt(i);
                originalScales.RemoveAt(i);
                i--;
            }
        }

        m_MouseLook.UpdateCursorLock();
    }

    void LateUpdate()
    {
        if (takeHiResShot)
        {
            int resWidth = 1920*6;
            int resHeight = 1080*6;
            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            Camera.main.targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            Camera.main.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            Camera.main.targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToJPG(100);
            System.IO.File.WriteAllBytes("Screenshot.jpg", bytes);
            takeHiResShot = false;
        }
    }
}
