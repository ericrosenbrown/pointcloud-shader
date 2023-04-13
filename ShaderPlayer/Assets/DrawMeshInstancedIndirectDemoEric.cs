using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class DrawMeshInstancedIndirectDemoEric : MonoBehaviour
{
    public float range;

    public Texture2D color_image;

    public Material material;

    public ComputeShader compute;
    private ComputeBuffer meshPropertiesBuffer;
    private ComputeBuffer argsBuffer;

    private Mesh mesh;
    private Bounds bounds;

    public float noise_range;

    private int total_population;
    private int population;
    public int downsample;
    private int height;
    private int width;

    public float size_scale; //hack to current pointcloud viewing

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
        //size_scale = 0.002f;
        width = 640;
        height = 480;
        total_population = height * width;
        population = (int) (total_population / downsample);
        Mesh mesh = CreateQuad(size_scale, size_scale,size_scale);
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

    private MeshProperties[] GetProperties()
    {
        // Initialize buffer with the given population.
        MeshProperties[] properties = new MeshProperties[population];


        int x;
        int y;
        int depth_idx;
        int i;
        for (int pop_i = 0; pop_i < population; pop_i++)
        {
            i = pop_i * downsample;
            MeshProperties props = new MeshProperties();
            x = i % (width);
            y = (int)Mathf.Floor(i / width);
            depth_idx = (width * (height - y - 1)) + x;

            Vector3 position;

            if (depth_ar[depth_idx] == 0)
            {
                position = new Vector3(10000, 1000, 1000);

                props.mat = Matrix4x4.TRS(new Vector3(0,0,0), Quaternion.Euler(0,0,0), Vector3.one * 0);
                //props.color = Color.Lerp(Color.red, Color.blue, Random.value);

                props.color = new Vector4(0, 0, 0, 0);

                properties[pop_i] = props;
                continue;

            }
            else
            {
                position = pixel_to_vision_frame(x, y, depth_ar[depth_idx]); //TODO: Get 4x4 matrix instead
            }

            Quaternion rotation = Quaternion.Euler(0, 0, 0);
            Vector3 scale = Vector3.one * 1;
            Vector3 some_noise = new Vector3(Random.Range(-noise_range, noise_range), Random.Range(-noise_range, noise_range), Random.Range(-noise_range, noise_range));
            props.mat = Matrix4x4.TRS(position+some_noise, rotation, scale);
            //props.color = Color.Lerp(Color.red, Color.blue, Random.value);

            props.color = color_image.GetPixel(x, y);

            properties[pop_i] = props;
        }

        return (properties);
    }

    private void InitializeBuffers()
    {
        int kernel = compute.FindKernel("CSMain");

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


        meshPropertiesBuffer = new ComputeBuffer(population, MeshProperties.Size());
        meshPropertiesBuffer.SetData(GetProperties());

        SetProperties();
        SetGOPosition();
    }

    private void SetGOPosition()
    {
        compute.SetMatrix("_GOPose", Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(1, 1, 1)));
    }

    private void SetProperties()
    {
        int kernel = compute.FindKernel("CSMain");

        meshPropertiesBuffer.SetData(GetProperties());
        material.SetBuffer("_Properties", meshPropertiesBuffer);
        compute.SetBuffer(kernel, "_Properties", meshPropertiesBuffer);
    }

    private void Update()
    {
        int kernel = compute.FindKernel("CSMain");
        //SetProperties enables point cloud to move when game object moves, but is laggier due to redrawing. Just comment it out for performance improvement;
        SetProperties();
        SetGOPosition();

        // We used to just be able to use `population` here, but it looks like a Unity update imposed a thread limit (65535) on my device.
        // This is probably for the best, but we have to do some more calculation.  Divide population by numthreads.x (declared in compute shader).
        compute.Dispatch(kernel, Mathf.CeilToInt(population / 64f), 1, 1);

        Graphics.DrawMeshInstancedIndirect(mesh, 0, material, bounds, argsBuffer);
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

    private Mesh CreateQuad(float width = 1f, float height = 1f, float depth = 1f)
    {
        // Create a quad mesh.
        var mesh = new Mesh();

        float w = width * .5f;
        float h = height * .5f;
        float d = depth * .5f;

        var vertices = new Vector3[8] {
            new Vector3(-w, -h, -d),
            new Vector3(w, -h, -d),
            new Vector3(w, h, -d),
            new Vector3(-w, h, -d),
            new Vector3(-w, -h, d),
            new Vector3(w, -h, d),
            new Vector3(w, h, d),
            new Vector3(-w, h, d)
        };

        var tris = new int[3*2*6] {
            0, 3, 1,
            3, 2, 1,

            0,4,5,
            0,5,1,

            1,5,2,
            2,5,6,

            7,3,6,
            3,6,2,

            0,4,3,
            4,7,3,

            4,7,5,
            7,5,6
        };

        var normals = new Vector3[8] {
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,

        };

        var uv = new Vector2[8] {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
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