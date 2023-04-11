using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class DrawMeshInstancedIndirectDemoEric : MonoBehaviour
{
    public float range;

    public Texture2D color_image;

    public Material material;

    private ComputeBuffer meshPropertiesBuffer;
    private ComputeBuffer argsBuffer;

    private Mesh mesh;
    private Bounds bounds;

    private int population;
    private int height;
    private int width;

    private float size_scale; //hack to current pointcloud viewing

    private float[] depth_ar;

    // Mesh Properties struct to be read from the GPU.
    // Size() is a convenience funciton which returns the stride of the struct.
    private struct MeshProperties
    {
        public Matrix4x4 mat;
        public Vector4 color;

        public static int Size()
        {
            return
                sizeof(float) * 4 * 4 + // matrix;
                sizeof(float) * 4;      // color;
        }
    }

    private void Setup()
    {
        size_scale = 0.02f;
        width = 640;
        height = 480;
        population = height * width;
        Mesh mesh = CreateQuad(size_scale, size_scale);
        //Mesh mesh = CreateQuad(0.01f,0.01f);
        this.mesh = mesh;

        depth_ar = new float[height * width];
        int counter = 0;

        StreamReader inp_stm = new StreamReader("./Assets/color2_depth_unity.txt");

        while (!inp_stm.EndOfStream)
        {
            string inp_ln = inp_stm.ReadLine();
            string[] split_arr = inp_ln.Split(',');
            foreach (var spli in split_arr)
            {
                depth_ar[counter] = float.Parse(spli);
                counter += 1;
                //Debug.Log(spli);
            }
            // Do Something with the input. 
        }

        inp_stm.Close();

        // Boundary surrounding the meshes we will be drawing.  Used for occlusion.
        bounds = new Bounds(transform.position, Vector3.one * (range + 1));

        InitializeBuffers();
    }

    private void InitializeBuffers()
    {
        // Argument buffer used by DrawMeshInstancedIndirect.
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // Arguments for drawing mesh.
        // 0 == number of triangle indices, 1 == population, others are only relevant if drawing submeshes.
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)population;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);

        // Initialize buffer with the given population.
        MeshProperties[] properties = new MeshProperties[population];


        int x;
        int y;
        int depth_idx;
        for (int i = 0; i < population; i++)
        {
            MeshProperties props = new MeshProperties();
            x = i % (width);
            y = (int)Mathf.Floor(i / width);
            depth_idx = (width * (height - y - 1)) + x;

            Vector3 position;

            if (depth_ar[depth_idx] == 0)
            {
                position = new Vector3(10000,1000,1000);
            }
            else
            {
                position = pixel_to_vision_frame(x, y, depth_ar[depth_idx]);
                //position = new Vector3(x* size_scale, y* size_scale, depth_ar[depth_idx]*100* size_scale);
            }

            Quaternion rotation = Quaternion.Euler(0,0,0);
            Vector3 scale = Vector3.one*1;

            props.mat = Matrix4x4.TRS(position, rotation, scale);
            //props.color = Color.Lerp(Color.red, Color.blue, Random.value);

            props.color = color_image.GetPixel(x, y);

            properties[i] = props;
        }

        meshPropertiesBuffer = new ComputeBuffer(population, MeshProperties.Size());
        meshPropertiesBuffer.SetData(properties);
        material.SetBuffer("_Properties", meshPropertiesBuffer);
    }

    private Vector3 pixel_to_vision_frame(int i,int j, float depth)
    {
        int CX = 320;
        int CY = 240;

        float FX = (float) 552.029101;
        float FY = (float) 552.029101;
        
        float x =  (j - CX) * depth / FX;
        float y = (i - CY) * depth / FY;

        Vector3 ret = new Vector3(x, y, depth);
        return (ret);

    }

    private Mesh CreateQuad(float width = 1f, float height = 1f)
    {
        // Create a quad mesh.
        var mesh = new Mesh();

        float w = width * .5f;
        float h = height * .5f;
        var vertices = new Vector3[4] {
            new Vector3(-w, -h, 0),
            new Vector3(w, -h, 0),
            new Vector3(-w, h, 0),
            new Vector3(w, h, 0)
        };

        var tris = new int[6] {
            // lower left tri.
            0, 2, 1,
            // lower right tri
            2, 3, 1
        };

        var normals = new Vector3[4] {
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
        };

        var uv = new Vector2[4] {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
        };

        mesh.vertices = vertices;
        mesh.triangles = tris;
        mesh.normals = normals;
        mesh.uv = uv;

        return mesh;
    }

    private void Start()
    {
        Setup();
    }

    private void Update()
    {
        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
    }

    private void OnDisable()
    {
        // Release gracefully.
        if (meshPropertiesBuffer != null)
        {
            meshPropertiesBuffer.Release();
        }
        meshPropertiesBuffer = null;

        if (argsBuffer != null)
        {
            argsBuffer.Release();
        }
        argsBuffer = null;
    }
}