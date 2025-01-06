using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using System;
using System.Linq;
using Unity.VisualScripting;
using Quaternion = UnityEngine.Quaternion;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Jobs;
public class Simulation_MultiCore : MonoBehaviour
{
    [Header("Simulation settings")]
    public int particles_num = 1000;
    public int rigid_bodies_num = 2;
    public float Gravity_force = 4f;
    public int Max_influence_radius = 1;
    public int Lg_chunk_dims = 5;
    public float Target_density = 50f;
    public float Pressure_multiplier = 200f;
    public float Near_density_multiplier = 20;
    public float Collision_damping_factor = 0.4f;
    public float Viscocity = 0.01f;
    public float Rb_Elasticity = 0.6f;

    [Header("Boundrary settings")]
    public int border_width = 40;
    public int border_height = 20;
    public int particle_spawner_dimensions = 15; // l x l
    public float border_thickness = 0.2f;
    public int Chunk_capacity = 100;
    public int Lg_chunk_capacity = 150;

    [Header("Rendering settings")]
    public float Program_speed = 1.5f;
    public float Time_step = -1;
    public float Particle_visual_size = 1.5f;
    public bool velocity_visuals;
    public bool Render_with_shader;
    public bool RenderMarchingSquares;

    [Header("Interaction settings")]
    public float Max_interaction_radius = 7;
    public float Interaction_power = 130;

    [Header("Advanced settings")]

    // Not in use ---
    public float Smooth_Max = 5f;
    public float Smooth_derivative_koefficient = 2.5f;
    public float Max_velocity = 5;
    // Not in use ---
    public float Look_ahead_factor = 0.035f;

    [Header("Object reference(s)")]
    public GameObject particle_prefab;
    public GameObject rigid_body_prefab;

    // rigid bodies ---
    private Transform[] rigid_bodies;
    private Renderer[] rigid_bodies_renderer;
    private Vector2[] rb_position;
    private Vector2[] rb_velocity;
    private float[] rb_radii;
    private float[] rb_mass;
    private float[] rb_influence_radii;
    public int[] lg_particle_chunks;
    private int[] lg_particle_chunks_template;
    public int Lg_chunk_amount_x;
    public int Lg_chunk_amount_y;
    private int  Lg_particle_chunks_tot_num;
    // rigid bodies ---

    private Transform[] particles;
    private Renderer[] particles_renderer;
    private GameObject simulation_boundary;
    private int Particle_chunks_tot_num;
    private int Chunk_amount_x = 0;
    private int Chunk_amount_y = 0;
    private Vector2 mouse_position;
    private bool left_mouse_button_down;
    private bool right_mouse_button_down;
    public Vector2[] position;
    private Vector2[] velocity;
    private Vector2[] last_velocity;
    private Vector2[] p_position;
    private float[] density;
    private float[] near_density;
    private int[] particle_chunks;
    private int[] particle_chunks_template;
    private float delta_time;

    Mesh mesh;
    private Vector3[] vertices = new Vector3[900];
    private int[] triangles = new int[900];
    private int tri_len = 0;
    public float PointMin;
    public Material MarchingSquaresMaterial;
    private int framecounter;
    private (uint, uint)[] particles_w_chunks;

    void Start()
    {
        // Create border
        Create_simulation_boundary();

        // Set camera position and size
        Camera.main.transform.position = new Vector3(border_width / 2, border_height / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(border_width * 0.75f, border_height * 1.5f);

        // initialize particle property arrays
        position = new Vector2[particles_num];
        velocity = new Vector2[particles_num];
        last_velocity = new Vector2[particles_num];
        p_position = new Vector2[particles_num];
        density = new float[particles_num];
        near_density = new float[particles_num];
        particles = new Transform[particles_num];

        // initialize rigid body property arrays
        rigid_bodies = new Transform[rigid_bodies_num];
        rigid_bodies_renderer = new Renderer[rigid_bodies_num];
        rb_position = new Vector2[rigid_bodies_num];
        rb_velocity = new Vector2[rigid_bodies_num];
        rb_radii = new float[rigid_bodies_num];
        rb_mass = new float[rigid_bodies_num];
        rb_influence_radii = new float[rigid_bodies_num];

        particles_w_chunks = new (uint, uint)[particles_num];

        if (velocity_visuals)
        {
            particles_renderer = new Renderer[particles_num];
        }

        Chunk_amount_x = border_width / Max_influence_radius;
        Chunk_amount_y = border_height / Max_influence_radius;
        Particle_chunks_tot_num = Chunk_amount_x * Chunk_amount_y * Chunk_capacity;
        particle_chunks = new int[Particle_chunks_tot_num];

        Lg_chunk_amount_x = border_width / Lg_chunk_dims;
        Lg_chunk_amount_y = border_height / Lg_chunk_dims;
        Lg_particle_chunks_tot_num = Lg_chunk_amount_x * Lg_chunk_amount_y * Lg_chunk_capacity;
        lg_particle_chunks = new int[Lg_particle_chunks_tot_num];

        particle_chunks_template = new int[Particle_chunks_tot_num];
        lg_particle_chunks_template = new int[Lg_particle_chunks_tot_num];
        Array.Fill(particle_chunks_template, -1);
        Array.Fill(lg_particle_chunks_template, -1);

        for (int i = 0; i < particles_num; i++)
        {
            position[i] = new(0.0f, 0.0f);
            velocity[i] = new(0.0f, 0.0f);
            p_position[i] = new(0.0f, 0.0f);
            density[i] = 0.0f;
            near_density[i] = 0.0f;
        }

        for (int i = 0; i < rigid_bodies_num; i++)
        {
            rb_position[i] = new(0.0f, 0.0f);
            rb_velocity[i] = new(0.0f, 0.0f);
            rb_radii[i] = 3.5f;
            rb_mass[i] = 10f;
            rb_influence_radii[i] = 0.5f;
        }

        // Create particles
        for (int i = 0; i < particles_num; i++)
        {
            Create_particle(i);
        }

        for (int i = 0; i < rigid_bodies_num; i++)
        {
            Create_rigid_body_sphere(i);
        }
        
        // Assign positions to particles
        for (int i = 0; i < particles_num; i++)
        {
            position[i] = Particle_spawn_position(i, particles_num);
        }

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;

        SortParticleIndices();
    }

    void Update()
    {
        if (Time_step == -1)
        {
            delta_time = Time.deltaTime * Program_speed;
        }
        else
        {
            delta_time = Time_step * Program_speed;
        }
        Vector3 xyz_mouse_pos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, -Camera.main.transform.position.z));
        mouse_position = new(xyz_mouse_pos.x, xyz_mouse_pos.y);
        left_mouse_button_down = Input.GetMouseButton(0);
        right_mouse_button_down = Input.GetMouseButton(1);

        // particle_chunks = (int[])particle_chunks_template.Clone();
        // lg_particle_chunks = (int[])lg_particle_chunks_template.Clone();
        // for (int i = 0; i < particles_num; i++)
        // {
        //     last_velocity[i] = velocity[i];
        // }
        // Job_populate_chunk_data();

        // 0.20 ms
        particle_chunks = (int[])particle_chunks_template.Clone();
        lg_particle_chunks = (int[])lg_particle_chunks_template.Clone();
        // 2.45 ms
        Populate_lg_particle_chunks();
        Populate_particle_chunks();


        // 2.23 ms
        Job_precalculate_densities();

        // 1.00 ms
        // implement repeated calls to enable simulations of complex rb interactions
        Job_rb_rb_collisions();

        if (rigid_bodies_num > 20)
        {
            // 6.00 ms (with 100 rb:s)
            Job_rb_particle_collisions();
        }
        else
        {
            // 0.63 ms (with 1 rb:s)
            Rb_particle_collisions();
        }

        // 2.44 ms
        Job_physics();

        // 7ms (with velocity_visuals on)
        Render();

        framecounter++;
        if (RenderMarchingSquares == true && framecounter == 300)
        {
            framecounter = 0;
            // marching squares

            // vertices = new Vector3[]{
            //     new Vector3(10,0,0),
            //     new Vector3(0,0,0),
            //     new Vector3(0,10,0)
            // };
            // triangles = new int[]{
            //     0, 1, 2
            // };

            float MarchScale = 0.5f;
            int MarchW = (int)(border_width / MarchScale);
            int MarchH = (int)(border_height / MarchScale);
            int TotIndices = MarchW * MarchH*9000;

            vertices = new Vector3[TotIndices];
            triangles = new int[TotIndices];

            Array.Clear(vertices, 0, vertices.Length);
            Array.Clear(triangles, 0, triangles.Length);
            tri_len = 0;

            int[] Points = new int[TotIndices];

            for (int x = 0; x < MarchW; x++)
            {
                for (int y = 0; y < MarchH; y++)
                {
                    Vector2 pos = new(MarchScale*x, MarchScale*y);
                    int Point = 1;
                    Points[x + y*MarchW] = Point;
                }
            }
            for (int x = 0; x < MarchW-1; x++)
            {
                for (int y = 0; y < MarchH-1; y++)
                {
                    // a = x, y
                    // b = x+1, y
                    // c = x, y+1
                    // d = x+1, y+1
                    int a = Points[x + y*MarchW];
                    int b = Points[x+1 + y*MarchW];
                    int c = Points[x+1 + y*MarchW+MarchW];
                    int d = Points[x + y*MarchW+MarchW];
                    int binaryTot = 1*a + 2*b + 4*c + 8*d;
                    // also add "parent" position
                    TriangleHash(binaryTot, MarchScale*2*x, MarchScale*2*y, MarchScale*2);
                }
            }

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.material = MarchingSquaresMaterial;

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
        }
    }

    void SortParticleIndices()
    {
        for (int i = 0; i < particles_num; i++)
        {
            particles_w_chunks[i].Item1 = (uint)i;
            uint particle_index = particles_w_chunks[i].Item1;
            Vector2 pos = position[particle_index];

            (uint, uint) chunk = ((uint)Math.Floor(pos.x / Max_influence_radius), (uint)Math.Floor(pos.y / Max_influence_radius));

            uint key = chunk.Item1 + chunk.Item2 * (uint)Chunk_amount_x;
            particles_w_chunks[i].Item2 = key;
        }
        Array.Sort(particles_w_chunks, (a, b) => a.Item2.CompareTo(b.Item2));
        for (int i = 0; i < particles_w_chunks.Length; i++)
        {
            Debug.Log(particles_w_chunks[i].Item2);
        }
        for (int i = 0; i < particles_w_chunks.Length; i++)
        {
            Debug.Log(particles_w_chunks[i].Item1);
        }
    }
    void TriangleHash(int TriangleID, float BaseX, float BaseY, float Scale)
    {
        switch (TriangleID)
        {
            case 0: break;
            case 1:
                vertices[tri_len] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX, BaseY, 0);
                vertices[tri_len + 2] = new Vector3(BaseX, BaseY + 0.5f * Scale, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 2:
                vertices[tri_len] = new Vector3(BaseX + Scale, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 3:
                vertices[tri_len] = new Vector3(BaseX, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 4:
                vertices[tri_len] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 5:
                vertices[tri_len] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 6:
                vertices[tri_len] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 7:
                vertices[tri_len] = new Vector3(BaseX, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + Scale, BaseY + Scale, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 8:
                vertices[tri_len] = new Vector3(BaseX, BaseY + Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX, BaseY + 0.5f * Scale, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 9:
                vertices[tri_len] = new Vector3(BaseX, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 10:
                vertices[tri_len] = new Vector3(BaseX, BaseY + 0.5f * Scale, 0);
                vertices[tri_len + 1] = new Vector3(BaseX, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX + 0.5f * Scale, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + Scale, BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX + Scale, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new Vector3(BaseX + Scale, BaseY, 0);
                vertices[tri_len + 1] = new Vector3(BaseX + 0.5f * Scale, BaseY + Scale, 0);
                vertices[tri_len + 2] = new Vector3(BaseX + Scale, BaseY + 0.5f * Scale, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 11:
                vertices[tri_len] = new(BaseX, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, 0 + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX + Scale, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + 0.5f * Scale, Scale + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX + 0.5f * Scale, Scale + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX + Scale, 0.5f * Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, 0 + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 12:
                vertices[tri_len] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX + Scale, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX, 0.5f * Scale + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX, 0.5f * Scale + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX + Scale, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, 0.5f * Scale + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 13:
                vertices[tri_len] = new(BaseX, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, Scale + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX + Scale, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, 0.5f * Scale + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX + Scale, 0.5f * Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + 0.5f * Scale, 0 + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 14:
                vertices[tri_len] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX + Scale, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, 0 + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX, 0.5f * Scale + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + 0.5f * Scale, 0 + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX + 0.5f * Scale, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, 0 + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            case 15:
                vertices[tri_len] = new(BaseX, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX + Scale, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, 0 + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;

                vertices[tri_len] = new(BaseX, 0 + BaseY, 0);
                vertices[tri_len + 1] = new(BaseX, Scale + BaseY, 0);
                vertices[tri_len + 2] = new(BaseX + Scale, Scale + BaseY, 0);
                triangles[tri_len] = tri_len;
                triangles[tri_len + 1] = tri_len + 1;
                triangles[tri_len + 2] = tri_len + 2;
                tri_len += 3;
                break;

            default:
                Debug.Log("triangleID invalid");
                break;
        }
    }

    void Render()
    {
        if (velocity_visuals)
        {
            for (int i = 0; i < particles_num; i++)
            {
                if (float.IsNaN(position[i].x) || float.IsNaN(position[i].y)){continue;}
                particles[i].position = position[i];
                particles_renderer[i].material.color = SetColorByFunction(velocity[i].magnitude);               
            }
        }
        else
        {
            for (int i = 0; i < particles_num; i++)
            {
                if (float.IsNaN(position[i].x) || float.IsNaN(position[i].y)){continue;}
                particles[i].position = position[i];
            }
        }

        for (int i = 0; i < rigid_bodies_num; i++)
        {
            rigid_bodies[i].position = rb_position[i];
        }
    }

    int InfluenceMarchingCubes(Vector2 Pos, float Min)
    {
        int in_chunk_x = (int)Mathf.Max((float)Math.Floor(Pos.x-0.01f / Lg_chunk_dims), 0);
        int in_chunk_y = (int)Mathf.Max((float)Math.Floor(Pos.y-0.01f / Lg_chunk_dims), 0);

        for (int x = -0; x <= 0; x++)
        {
            for (int y = -0; y <= 0; y++)
            {
                int cur_chunk_x = in_chunk_x + x;
                int cur_chunk_y = in_chunk_y + y;

                if (cur_chunk_x >= 0 && cur_chunk_x < Lg_chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Lg_chunk_amount_y)
                {
                    int start_i = cur_chunk_x * Lg_chunk_amount_y * Lg_chunk_capacity + cur_chunk_y * Lg_chunk_capacity;
                    int end_i = start_i + Lg_chunk_capacity;

                    for (int i = start_i; i < end_i; i++)
                    {
                        int particle_index = lg_particle_chunks[i];
                        if (particle_index == -1){continue;}

                        Vector2 distance = position[particle_index] - Pos;

                        float abs_distance = distance.magnitude;

                        if (abs_distance > Min){return 1;}
                    }
                }
            }
        }
        return 0;

        // float randomValue = Random.Range(0f, 1f);
        // if (randomValue < 0.6f) {return 1;}
        // return 0;
    }

    float SmoothMarchingCubes(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        return Mathf.Pow(1 - distance/Max_influence_radius, 2);
    }

    void Populate_lg_particle_chunks()
    {
        // parallel.For???????
        for (int i = 0; i < particles_num; i++)
        {
            // Set predicted positions - move this!!!!!!!!!!!!!!!!!!!
            p_position[i] = position[i] + velocity[i] * Look_ahead_factor;
            last_velocity[i] = velocity[i];
                                                            // changed to Max_influence_radius
            int in_chunk_x = (int)Math.Floor(position[i].x / Max_influence_radius);
            int in_chunk_y = (int)Math.Floor(position[i].y / Max_influence_radius);

            int xy_index = in_chunk_x * Lg_chunk_amount_y * Lg_chunk_capacity + in_chunk_y * Lg_chunk_capacity;
            int z_index = 0;
            while (lg_particle_chunks[xy_index + z_index] != -1 && z_index < Lg_chunk_capacity - 1)
            {
                z_index += 1;
            }
            // Add to list if space is available
            if (z_index < Lg_chunk_capacity - 1)
            {
                lg_particle_chunks[xy_index + z_index] = i;
            }
            else
            {
                Debug.Log("Lg chunk particle capacity reached");
            }
        }
    }

    void Populate_particle_chunks()
    {
        // parallel.For???????
        for (int i = 0; i < particles_num; i++)
        {
            int in_chunk_x = (int)Math.Floor(p_position[i].x / Max_influence_radius);
            int in_chunk_y = (int)Math.Floor(p_position[i].y / Max_influence_radius);
            float relative_position_x = p_position[i].x / Max_influence_radius % 1;
            float relative_position_y = p_position[i].y / Max_influence_radius % 1;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    // current chunk
                    int cur_chunk_x = in_chunk_x + x;
                    int cur_chunk_y = in_chunk_y + y;

                    if (cur_chunk_x >= 0 && cur_chunk_x < Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Chunk_amount_y)
                    {
                        if (1 > (x-relative_position_x)*(x-relative_position_x)+(y-relative_position_y)*(y-relative_position_y) ||
                            1 > (x+1-relative_position_x)*(x+1-relative_position_x)+(y-relative_position_y)*(y-relative_position_y)||
                            1 > (x-relative_position_x)*(x-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y) ||
                            1 > (x+1-relative_position_x)*(x+1-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y))
                        {
                            int xy_index = cur_chunk_x * Chunk_amount_y * Chunk_capacity + cur_chunk_y * Chunk_capacity;
                            int z_index = 0;
                            while (particle_chunks[xy_index + z_index] != -1 && z_index < Chunk_capacity)
                            {
                                z_index += 1;
                            }
                            // Add to list if space is available
                            if (z_index < Chunk_capacity)
                            {
                                particle_chunks[xy_index + z_index] = i;
                            }
                            else
                            {
                                Debug.Log("Chunk particle capacity reached");
                            }
                        }
                    }
                }
            }
        }
    }

    void Job_populate_chunk_data()
    {
        NativeArray<int> n_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);
        NativeArray<int> n_lg_particle_chunks = new NativeArray<int>(Lg_particle_chunks_tot_num, Allocator.TempJob);
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);

        NativeList<JobHandle> job_handle_list = new NativeList<JobHandle>(Allocator.Temp);

        n_position.CopyFrom(position);
        n_velocity.CopyFrom(velocity);
        n_particle_chunks.CopyFrom(particle_chunks);
        n_lg_particle_chunks.CopyFrom(lg_particle_chunks);

        Populate_particle_chunks_job populate_particle_chunks_job = new Populate_particle_chunks_job {
            velocity = n_velocity,
            position = n_position,
            particle_chunks = n_particle_chunks,
            p_position = n_p_position,
            Max_influence_radius = Max_influence_radius,
            Chunk_amount_x = Chunk_amount_x,
            Chunk_amount_y = Chunk_amount_y,
            Chunk_capacity = Chunk_capacity,
            particles_num = particles_num,
            Look_ahead_factor = Look_ahead_factor
        };
        JobHandle handle_1 = populate_particle_chunks_job.Schedule();
        job_handle_list.Add(handle_1);

        Populate_lg_particle_chunks_job populate_lg_particle_chunks_job = new Populate_lg_particle_chunks_job {
            lg_particle_chunks = n_lg_particle_chunks,
            position = n_position,
            Max_influence_radius = Max_influence_radius,
            Lg_chunk_amount_y = Lg_chunk_amount_y,
            Lg_chunk_capacity = Lg_chunk_capacity,
            particles_num = particles_num,
        };
        JobHandle handle_2 = populate_lg_particle_chunks_job.Schedule();
        job_handle_list.Add(handle_2);

        JobHandle.CompleteAll(job_handle_list.AsArray());

        for (int i = 0; i < Particle_chunks_tot_num; i++)
        {
            particle_chunks[i] = n_particle_chunks[i];
        }
        for (int i = 0; i < Lg_particle_chunks_tot_num; i++)
        {
            lg_particle_chunks[i] = n_lg_particle_chunks[i];
        }
        for (int i = 0; i < particles_num; i++)
        {
            p_position[i] = n_p_position[i];
        }

        n_particle_chunks.Dispose();
        n_lg_particle_chunks.Dispose();
        n_p_position.Dispose();
        n_position.Dispose();
        n_velocity.Dispose();
        job_handle_list.Dispose();
    }

    void Job_precalculate_densities()
    {
        // MAYBE - MIGHT NEED TO RESET THE VALUES EACH ITERATION --- PLACE SOMEWHERE ELSE TO AVOID FOR LOOP WITH ca 50000 elements each update iteration !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<float> n_near_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);

        n_new_particle_chunks.CopyFrom(particle_chunks);
        n_p_position.CopyFrom(p_position);
        n_position.CopyFrom(position);

        Calculate_density_job calculate_density_job = new Calculate_density_job {
            Max_influence_radius = Max_influence_radius,
            Smooth_Max = Smooth_Max,
            Smooth_derivative_koefficient = Smooth_derivative_koefficient,
            Chunk_amount_y = Chunk_amount_y,
            Chunk_amount_x = Chunk_amount_x,
            Chunk_capacity = Chunk_capacity,
            p_position = n_p_position,
            position = n_position,
            particle_chunks = n_new_particle_chunks,
            density = n_density,
            near_density = n_near_density
        };

        JobHandle jobhandle = calculate_density_job.Schedule(particles_num, 32);

        jobhandle.Complete();

        for (int i = 0; i < particles_num; i++)
        {
            density[i] = n_density[i];
            near_density[i] = n_near_density[i];
        }
 
        n_p_position.Dispose();
        n_position.Dispose();
        n_density.Dispose();
        n_near_density.Dispose();
        n_new_particle_chunks.Dispose();

    }

    void Job_physics()
    {
        NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_p_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_last_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<float> n_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<float> n_near_density = new NativeArray<float>(particles_num, Allocator.TempJob);
        NativeArray<int> n_new_particle_chunks = new NativeArray<int>(Particle_chunks_tot_num, Allocator.TempJob);

        n_velocity.CopyFrom(velocity);
        n_position.CopyFrom(position);
        n_p_position.CopyFrom(p_position);
        n_density.CopyFrom(density);
        n_near_density.CopyFrom(near_density);
        n_last_velocity.CopyFrom(last_velocity);
        n_new_particle_chunks.CopyFrom(particle_chunks);

        Calculate_physics_job calculate_physics_job = new Calculate_physics_job {
            Gravity_force = Gravity_force,
            delta_time = delta_time,
            border_height = border_height,
            border_width = border_width,
            border_thickness = border_thickness,
            Collision_damping_factor = Collision_damping_factor,
            Smooth_Max = Smooth_Max,
            Smooth_derivative_koefficient = Smooth_derivative_koefficient,
            Target_density = Target_density,
            Pressure_multiplier = Pressure_multiplier,
            Max_influence_radius = Max_influence_radius,
            Viscocity = Viscocity,
            Max_interaction_radius = Max_interaction_radius,
            Chunk_amount_y = Chunk_amount_y,
            Chunk_capacity = Chunk_capacity,
            Near_density_multiplier = Near_density_multiplier,
            Interaction_power = Interaction_power,
            Max_velocity = Max_velocity,
            mouse_position = mouse_position,
            left_mouse_button_down = left_mouse_button_down,
            right_mouse_button_down = right_mouse_button_down,
            velocity = n_velocity,
            position = n_position,
            p_position = n_p_position,
            density = n_density,
            near_density = n_near_density,
            last_velocity = n_last_velocity,
            particle_chunks = n_new_particle_chunks
        };

        JobHandle jobhandle = calculate_physics_job.Schedule(particles_num, 32);

        jobhandle.Complete();

        for (int i = 0; i < particles_num; i++)
        {
            velocity[i] = n_velocity[i];
            position[i] = n_position[i];
        }
 
        n_velocity.Dispose();
        n_position.Dispose();
        n_p_position.Dispose();
        n_density.Dispose();
        n_near_density.Dispose();
        n_last_velocity.Dispose();
        n_new_particle_chunks.Dispose();
    }

    void Create_particle(int particle_index)
    {
        GameObject particle = Instantiate(particle_prefab, new(0,0,0), Quaternion.identity);
        particle.transform.localScale = new Vector3(Particle_visual_size, Particle_visual_size, Particle_visual_size);
        particle.transform.parent = transform;
        particles[particle_index] = particle.transform;
        if (velocity_visuals)
        {
            particles_renderer[particle_index] = particle.GetComponent<Renderer>();
        }
    }

    Color SetColorByFunction(float gradient_value)
    {
        gradient_value = Mathf.Min(gradient_value, 5f);

        float normalizedValue = gradient_value / 5f;

        Color newColor = Color.Lerp(Color.blue, Color.red, normalizedValue);

        return newColor;
    }

    void Create_simulation_boundary()

    {
        // Create an empty GameObject for the border
        simulation_boundary = new GameObject("SimulationBoundary");
        simulation_boundary.transform.parent = transform;

        // Add a LineRenderer component to represent the border
        LineRenderer lineRenderer = simulation_boundary.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 5;

        lineRenderer.SetPositions(new Vector3[]
        {
            new(0f, 0f, 0f),
            new(border_width, 0f, 0f),
            new(border_width, border_height, 0f),
            new(0f, border_height, 0f),
            new(0f, 0f, 0f),
        });

        // Optional: Set LineRenderer properties (material, color, width, etc.)
        // lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.green;
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
    }

    Vector2 Particle_spawn_position(int particle_index, int max_index)
    {
        float x = (border_width - particle_spawner_dimensions) / 2 + Mathf.Floor(particle_index % Mathf.Sqrt(max_index)) * (particle_spawner_dimensions / Mathf.Sqrt(max_index));
        float y = (border_height - particle_spawner_dimensions) / 2 + Mathf.Floor(particle_index / Mathf.Sqrt(max_index)) * (particle_spawner_dimensions / Mathf.Sqrt(max_index));
        if (particle_spawner_dimensions > border_width || particle_spawner_dimensions > border_height)
        {
            throw new ArgumentException("Particle spawn dimensions larger than either border_width or border_height");
        }
        return new Vector2(x, y);
    }

    void Create_rigid_body_sphere(int rb_index)
    {
        GameObject rigid_body_object = Instantiate(particle_prefab, new(0,0,0), Quaternion.identity);
        rb_position[rb_index] = new(border_width / 2 + rb_index*8 - rigid_bodies_num*4, border_height - 1);
        float rigid_body_scale = 6.3f;
        rigid_body_object.transform.localScale = new Vector3(rigid_body_scale, rigid_body_scale, rigid_body_scale);
        rigid_body_object.transform.parent = transform;
        rigid_bodies[rb_index] = rigid_body_object.transform;
        rigid_bodies_renderer[rb_index] = rigid_body_object.GetComponent<Renderer>();
        rigid_bodies_renderer[rb_index].material.color = Color.white;
    }

    void Job_rb_rb_collisions()
    {
        NativeArray<float> n_rb_radii = new NativeArray<float>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<float> n_rb_mass = new NativeArray<float>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<Vector2> n_rb_velocity = new NativeArray<Vector2>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<Vector2> n_rb_position = new NativeArray<Vector2>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<Vector2> copy_rb_velocity = new NativeArray<Vector2>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<Vector2> copy_rb_position = new NativeArray<Vector2>(rigid_bodies_num, Allocator.TempJob);

        n_rb_radii.CopyFrom(rb_radii);
        n_rb_mass.CopyFrom(rb_mass);
        n_rb_velocity.CopyFrom(rb_velocity);
        n_rb_position.CopyFrom(rb_position);
        copy_rb_velocity.CopyFrom(rb_velocity);
        copy_rb_position.CopyFrom(rb_position);

        Resolve_eventual_rb_rb_collisions_job resolve_eventual_rb_rb_collisions_job = new Resolve_eventual_rb_rb_collisions_job
        {
            Rb_Elasticity = Rb_Elasticity,
            rigid_bodies_num = rigid_bodies_num,
            rb_radii = n_rb_radii,
            rb_mass = n_rb_mass,
            copy_rb_velocity = copy_rb_velocity,
            copy_rb_position = copy_rb_position,
            rb_velocity = n_rb_velocity,
            rb_position = n_rb_position
        };
        //                                                                                           "1" might not be suitable for large amounts of rb:s
        JobHandle jobHandle = resolve_eventual_rb_rb_collisions_job.Schedule(rigid_bodies_num, 1);
        jobHandle.Complete();

        n_rb_velocity.CopyTo(rb_velocity);
        n_rb_position.CopyTo(rb_position);

        n_rb_radii.Dispose();
        n_rb_mass.Dispose();
        n_rb_velocity.Dispose();
        n_rb_position.Dispose();
        copy_rb_velocity.Dispose();
        copy_rb_position.Dispose();
    }

    void Job_rb_particle_collisions()
    {
        NativeArray<Vector2> n_position = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_velocity = new NativeArray<Vector2>(particles_num, Allocator.TempJob);
        NativeArray<Vector2> n_rb_velocity = new NativeArray<Vector2>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<Vector2> n_rb_position = new NativeArray<Vector2>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<float> n_rb_radii = new NativeArray<float>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<float> n_rb_mass = new NativeArray<float>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<int> n_lg_particle_chunks = new NativeArray<int>(Lg_particle_chunks_tot_num, Allocator.TempJob);
        NativeArray<Vector3_20x_array> position_data = new NativeArray<Vector3_20x_array>(rigid_bodies_num, Allocator.TempJob);
        NativeArray<Vector3_20x_array> velocity_delta_data = new NativeArray<Vector3_20x_array>(rigid_bodies_num, Allocator.TempJob);

        for (int i = 0; i < rigid_bodies_num; i++)
        {
            position_data[i] = Vector3_20x_array.CreateInitialized();
            velocity_delta_data[i] = Vector3_20x_array.CreateInitialized();
        }

        n_position.CopyFrom(position);
        n_rb_velocity.CopyFrom(rb_velocity);
        n_rb_position.CopyFrom(rb_position);
        n_velocity.CopyFrom(velocity);
        n_rb_radii.CopyFrom(rb_radii);
        n_rb_mass.CopyFrom(rb_mass);
        n_lg_particle_chunks.CopyFrom(lg_particle_chunks);

        Resolve_eventual_rb_particle_collisions_job resolve_eventual_rb_particle_collisions_job = new Resolve_eventual_rb_particle_collisions_job
        {
            Gravity_force = Gravity_force,
            delta_time = delta_time,
            Lg_chunk_dims = Lg_chunk_dims,
            Lg_chunk_amount_x = Lg_chunk_amount_x,
            Lg_chunk_amount_y = Lg_chunk_amount_y,
            Lg_chunk_capacity = Lg_chunk_capacity,
            Rb_Elasticity = Rb_Elasticity,
            Collision_damping_factor = Collision_damping_factor,
            border_height = border_height,
            border_width = border_width,
            border_thickness = border_thickness,
            rb_radii = n_rb_radii,
            rb_mass = n_rb_mass,
            lg_particle_chunks = n_lg_particle_chunks,
            position = n_position,
            velocity = n_velocity,
            rb_velocity = n_rb_velocity,
            rb_position = n_rb_position,
            position_data = position_data,
            velocity_delta_data = velocity_delta_data
        };

        JobHandle jobHandle = resolve_eventual_rb_particle_collisions_job.Schedule(rigid_bodies_num, 8);
        jobHandle.Complete();

        n_rb_velocity.CopyTo(rb_velocity);
        n_rb_position.CopyTo(rb_position);

        // The current system is very inefficient for returning data to particles! Otherwise, the job is efficient
        // perhaps inprove it by getting the custom struct directly, since Nativearray.getitem is the most expensive operation
        // otherwise, try redoing the data returning system
        for (int i = 0; i < position_data.Length; i++)
        {
            if ((int)position_data[i].s0.x != -1) { position[(int)position_data[i].s0.x] = new Vector2(position_data[i].s0.y, position_data[i].s0.z); } else { continue; }
            if ((int)position_data[i].s1.x != -1) { position[(int)position_data[i].s1.x] = new Vector2(position_data[i].s1.y, position_data[i].s1.z); } else { continue; }
            if ((int)position_data[i].s2.x != -1) { position[(int)position_data[i].s2.x] = new Vector2(position_data[i].s2.y, position_data[i].s2.z); } else { continue; }
            if ((int)position_data[i].s3.x != -1) { position[(int)position_data[i].s3.x] = new Vector2(position_data[i].s3.y, position_data[i].s3.z); } else { continue; }
            if ((int)position_data[i].s4.x != -1) { position[(int)position_data[i].s4.x] = new Vector2(position_data[i].s4.y, position_data[i].s4.z); } else { continue; }
            if ((int)position_data[i].s5.x != -1) { position[(int)position_data[i].s5.x] = new Vector2(position_data[i].s5.y, position_data[i].s5.z); } else { continue; }
            if ((int)position_data[i].s6.x != -1) { position[(int)position_data[i].s6.x] = new Vector2(position_data[i].s6.y, position_data[i].s6.z); } else { continue; }
            if ((int)position_data[i].s7.x != -1) { position[(int)position_data[i].s7.x] = new Vector2(position_data[i].s7.y, position_data[i].s7.z); } else { continue; }
            if ((int)position_data[i].s8.x != -1) { position[(int)position_data[i].s8.x] = new Vector2(position_data[i].s8.y, position_data[i].s8.z); } else { continue; }
            if ((int)position_data[i].s9.x != -1) { position[(int)position_data[i].s9.x] = new Vector2(position_data[i].s9.y, position_data[i].s9.z); } else { continue; }
            if ((int)position_data[i].s10.x != -1) { position[(int)position_data[i].s10.x] = new Vector2(position_data[i].s10.y, position_data[i].s10.z); } else { continue; }
            if ((int)position_data[i].s11.x != -1) { position[(int)position_data[i].s11.x] = new Vector2(position_data[i].s11.y, position_data[i].s11.z); } else { continue; }
            if ((int)position_data[i].s12.x != -1) { position[(int)position_data[i].s12.x] = new Vector2(position_data[i].s12.y, position_data[i].s12.z); } else { continue; }
            if ((int)position_data[i].s13.x != -1) { position[(int)position_data[i].s13.x] = new Vector2(position_data[i].s13.y, position_data[i].s13.z); } else { continue; }
            if ((int)position_data[i].s14.x != -1) { position[(int)position_data[i].s14.x] = new Vector2(position_data[i].s14.y, position_data[i].s14.z); } else { continue; }
            if ((int)position_data[i].s15.x != -1) { position[(int)position_data[i].s15.x] = new Vector2(position_data[i].s15.y, position_data[i].s15.z); } else { continue; }
            if ((int)position_data[i].s16.x != -1) { position[(int)position_data[i].s16.x] = new Vector2(position_data[i].s16.y, position_data[i].s16.z); } else { continue; }
            if ((int)position_data[i].s17.x != -1) { position[(int)position_data[i].s17.x] = new Vector2(position_data[i].s17.y, position_data[i].s17.z); } else { continue; }
            if ((int)position_data[i].s18.x != -1) { position[(int)position_data[i].s18.x] = new Vector2(position_data[i].s18.y, position_data[i].s18.z); } else { continue; }
            if ((int)position_data[i].s19.x != -1) { position[(int)position_data[i].s19.x] = new Vector2(position_data[i].s19.y, position_data[i].s19.z); }
        }
        int[] applied_indices = {};
        for (int i = 0; i < velocity_delta_data.Length; i++)
        {
            if ((int)velocity_delta_data[i].s0.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s0.x)) { velocity[(int)velocity_delta_data[i].s0.x] += new Vector2(velocity_delta_data[i].s0.y, velocity_delta_data[i].s0.z); applied_indices.Append((int)velocity_delta_data[i].s0.x);} else { continue; }
            if ((int)velocity_delta_data[i].s1.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s1.x)) { velocity[(int)velocity_delta_data[i].s1.x] += new Vector2(velocity_delta_data[i].s1.y, velocity_delta_data[i].s1.z); applied_indices.Append((int)velocity_delta_data[i].s1.x);} else { continue; }
            if ((int)velocity_delta_data[i].s2.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s2.x)) { velocity[(int)velocity_delta_data[i].s2.x] += new Vector2(velocity_delta_data[i].s2.y, velocity_delta_data[i].s2.z); applied_indices.Append((int)velocity_delta_data[i].s2.x);} else { continue; }
            if ((int)velocity_delta_data[i].s3.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s3.x)) { velocity[(int)velocity_delta_data[i].s3.x] += new Vector2(velocity_delta_data[i].s3.y, velocity_delta_data[i].s3.z); applied_indices.Append((int)velocity_delta_data[i].s3.x);} else { continue; }
            if ((int)velocity_delta_data[i].s4.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s4.x)) { velocity[(int)velocity_delta_data[i].s4.x] += new Vector2(velocity_delta_data[i].s4.y, velocity_delta_data[i].s4.z); applied_indices.Append((int)velocity_delta_data[i].s4.x);} else { continue; }
            if ((int)velocity_delta_data[i].s5.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s5.x)) { velocity[(int)velocity_delta_data[i].s5.x] += new Vector2(velocity_delta_data[i].s5.y, velocity_delta_data[i].s5.z); applied_indices.Append((int)velocity_delta_data[i].s5.x);} else { continue; }
            if ((int)velocity_delta_data[i].s6.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s6.x)) { velocity[(int)velocity_delta_data[i].s6.x] += new Vector2(velocity_delta_data[i].s6.y, velocity_delta_data[i].s6.z); applied_indices.Append((int)velocity_delta_data[i].s6.x);} else { continue; }
            if ((int)velocity_delta_data[i].s7.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s7.x)) { velocity[(int)velocity_delta_data[i].s7.x] += new Vector2(velocity_delta_data[i].s7.y, velocity_delta_data[i].s7.z); applied_indices.Append((int)velocity_delta_data[i].s7.x);} else { continue; }
            if ((int)velocity_delta_data[i].s8.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s8.x)) { velocity[(int)velocity_delta_data[i].s8.x] += new Vector2(velocity_delta_data[i].s8.y, velocity_delta_data[i].s8.z); applied_indices.Append((int)velocity_delta_data[i].s8.x);} else { continue; }
            if ((int)velocity_delta_data[i].s9.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s9.x)) { velocity[(int)velocity_delta_data[i].s9.x] += new Vector2(velocity_delta_data[i].s9.y, velocity_delta_data[i].s9.z); applied_indices.Append((int)velocity_delta_data[i].s9.x);} else { continue; }
            if ((int)velocity_delta_data[i].s10.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s10.x)) { velocity[(int)velocity_delta_data[i].s10.x] += new Vector2(velocity_delta_data[i].s10.y, velocity_delta_data[i].s10.z); applied_indices.Append((int)velocity_delta_data[i].s10.x);} else { continue; }
            if ((int)velocity_delta_data[i].s11.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s11.x)) { velocity[(int)velocity_delta_data[i].s11.x] += new Vector2(velocity_delta_data[i].s11.y, velocity_delta_data[i].s11.z); applied_indices.Append((int)velocity_delta_data[i].s11.x);} else { continue; }
            if ((int)velocity_delta_data[i].s12.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s12.x)) { velocity[(int)velocity_delta_data[i].s12.x] += new Vector2(velocity_delta_data[i].s12.y, velocity_delta_data[i].s12.z); applied_indices.Append((int)velocity_delta_data[i].s12.x);} else { continue; }
            if ((int)velocity_delta_data[i].s13.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s13.x)) { velocity[(int)velocity_delta_data[i].s13.x] += new Vector2(velocity_delta_data[i].s13.y, velocity_delta_data[i].s13.z); applied_indices.Append((int)velocity_delta_data[i].s13.x);} else { continue; }
            if ((int)velocity_delta_data[i].s14.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s14.x)) { velocity[(int)velocity_delta_data[i].s14.x] += new Vector2(velocity_delta_data[i].s14.y, velocity_delta_data[i].s14.z); applied_indices.Append((int)velocity_delta_data[i].s14.x);} else { continue; }
            if ((int)velocity_delta_data[i].s15.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s15.x)) { velocity[(int)velocity_delta_data[i].s15.x] += new Vector2(velocity_delta_data[i].s15.y, velocity_delta_data[i].s15.z); applied_indices.Append((int)velocity_delta_data[i].s15.x);} else { continue; }
            if ((int)velocity_delta_data[i].s16.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s16.x)) { velocity[(int)velocity_delta_data[i].s16.x] += new Vector2(velocity_delta_data[i].s16.y, velocity_delta_data[i].s16.z); applied_indices.Append((int)velocity_delta_data[i].s16.x);} else { continue; }
            if ((int)velocity_delta_data[i].s17.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s17.x)) { velocity[(int)velocity_delta_data[i].s17.x] += new Vector2(velocity_delta_data[i].s17.y, velocity_delta_data[i].s17.z); applied_indices.Append((int)velocity_delta_data[i].s17.x);} else { continue; }
            if ((int)velocity_delta_data[i].s18.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s18.x)) { velocity[(int)velocity_delta_data[i].s18.x] += new Vector2(velocity_delta_data[i].s18.y, velocity_delta_data[i].s18.z); applied_indices.Append((int)velocity_delta_data[i].s18.x);} else { continue; }
            if ((int)velocity_delta_data[i].s19.x != -1 && !applied_indices.Contains((int)velocity_delta_data[i].s19.x)) { velocity[(int)velocity_delta_data[i].s19.x] += new Vector2(velocity_delta_data[i].s19.y, velocity_delta_data[i].s19.z); applied_indices.Append((int)velocity_delta_data[i].s19.x);}
        }

        n_position.Dispose();
        n_velocity.Dispose();
        n_rb_velocity.Dispose();
        n_rb_position.Dispose();
        n_rb_radii.Dispose();
        n_rb_mass.Dispose();
        n_lg_particle_chunks.Dispose();
        position_data.Dispose();
        velocity_delta_data.Dispose();
    }

    void Rb_particle_collisions()
    {
        
        // velocities and positions of particles are currently updated before this function triggers

        for (int rb_i = 0; rb_i < rigid_bodies_num; rb_i++)
        {
            // Here goes all forces acting on the rb_sphere ---
            rb_velocity[rb_i].y -= Gravity_force * delta_time;
            rb_position[rb_i] += rb_velocity[rb_i] * delta_time;
            rigid_bodies[rb_i].position = rb_position[rb_i];
            // Here goes all forces acting on the rb_sphere ---

            int in_chunk_x = (int)Math.Floor(rb_position[rb_i].x / Lg_chunk_dims);
            int in_chunk_y = (int)Math.Floor(rb_position[rb_i].y / Lg_chunk_dims);

            int particle_search_radius = (int)Math.Ceiling(rb_radii[rb_i]);

            for (int x = -particle_search_radius; x <= particle_search_radius; x++)
            {
                for (int y = -particle_search_radius; y <= particle_search_radius; y++)
                {
                    int cur_chunk_x = in_chunk_x + x;
                    int cur_chunk_y = in_chunk_y + y;

                    if (cur_chunk_x >= 0 && cur_chunk_x < Lg_chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Lg_chunk_amount_y)
                    {
                        int start_i = cur_chunk_x * Lg_chunk_amount_y * Lg_chunk_capacity + cur_chunk_y * Lg_chunk_capacity;
                        int end_i = start_i + Lg_chunk_capacity;

                        for (int i = start_i; i < end_i; i++)
                        {
                            int particle_index = lg_particle_chunks[i];
                            if (particle_index == -1){continue;}

                            Vector2 distance = position[particle_index] - rb_position[rb_i];

                            float abs_distance = distance.magnitude;

                            if (abs_distance >= rb_radii[rb_i]){continue;}

                            Vector2 velocity_diff = velocity[particle_index] - rb_velocity[rb_i];

                            Vector2 norm_distance = distance.normalized;

                            Vector2 relative_collision_position = rb_radii[rb_i] * norm_distance;

                            Vector2 wall_direction = new(norm_distance.y, -norm_distance.x);

                            // v = (a,b)
                            // u = (c,d) (u is normalized)
                            // => v':
                            // v'_x = (2c^2-1)*a + 2cdb
                            // v'_y = 2cda + (2d^2-1)b
                            // mirror velocity_diff through norm_distance
                            float a = velocity_diff.x;
                            float b = velocity_diff.y;
                            float c = norm_distance.x;
                            float d = norm_distance.y;

                            float mirror_velocity_diff_x = (2*c*c-1)*a + 2*c*d*b;
                            float mirror_velocity_diff_y = 2*c*d*a + (2*d*d-1)*b;
                            Vector2 mirror_velocity_diff = new(-mirror_velocity_diff_x, -mirror_velocity_diff_y);

                            Vector2 delta_particle_velocity = mirror_velocity_diff - velocity_diff;

                            Vector2 exchanged_momentum = delta_particle_velocity * Rb_Elasticity;

                            // Not currently in use. Also, this is not equal to the energy loss by the collision since temperature_energy is not propertional to velocity_energy;
                            float overflow_momentum = (delta_particle_velocity * (1-Rb_Elasticity)).magnitude;

                            // v = (a,b)
                            // u = (c,d) (u is normalized)
                            // => v_projected:
                            // v_projected_x = (ac+bd)*c
                            // v_projected_y = (ac+bd)*d
                            // Momentum and circular impulses:

                            // Vector2 center_impulse = exchanged_momentum [proj to] norm_distance
                            // Vector2 rotation_impulse = exchanged_momentum [proj to] wall_direction
                            // but these methods are not currently used since, for circular objects, center_impulse = exchanged_momentum, and, rotation_impulse = 0.
                            // thus:
                            Vector2 center_impulse = exchanged_momentum;

                            // v = I / m
                            rb_velocity[rb_i] -= center_impulse / rb_mass[rb_i];

                            // place particle outside of circle object
                            position[particle_index] = rb_position[rb_i] + relative_collision_position;
                            // position[i] = new(Mathf.Max(Mathf.Min(position[i].x, border_width), 0), Mathf.Max(Mathf.Min(position[i].y, border_height), 0));


                            //                               (* mass) but mass = 1
                            velocity[particle_index] += exchanged_momentum;
                        };
                    }
                }
            }


            border_thickness = -0.5f;

            if (rb_position[rb_i].y > border_height - border_thickness - rb_radii[rb_i])
            {
                rb_velocity[rb_i] = new Vector2(rb_velocity[rb_i].x, -Mathf.Abs(rb_velocity[rb_i].y * Collision_damping_factor));
                rb_position[rb_i] = new Vector2(rb_position[rb_i].x, border_height - border_thickness - rb_radii[rb_i]);
            }
            if (rb_position[rb_i].y < border_thickness + rb_radii[rb_i])
            {
                rb_velocity[rb_i] = new Vector2(rb_velocity[rb_i].x, +Mathf.Abs(rb_velocity[rb_i].y * Collision_damping_factor));
                rb_position[rb_i] = new Vector2(rb_position[rb_i].x, border_thickness + rb_radii[rb_i]);
            }
            if (rb_position[rb_i].x > border_width - border_thickness - rb_radii[rb_i])
            {
                rb_velocity[rb_i] = new Vector2(-Mathf.Abs(rb_velocity[rb_i].x * Collision_damping_factor), rb_velocity[rb_i].y);
                rb_position[rb_i] = new Vector2(border_width - border_thickness - rb_radii[rb_i], rb_position[rb_i].y);
            }
            if (rb_position[rb_i].x < border_thickness + rb_radii[rb_i])
            {
                rb_velocity[rb_i] = new Vector2(+Mathf.Abs(rb_velocity[rb_i].x * Collision_damping_factor), rb_velocity[rb_i].y);
                rb_position[rb_i] = new Vector2(border_thickness + rb_radii[rb_i], rb_position[rb_i].y);
            }

            border_thickness = 0.2f;
            
        }
    }

    float Smooth_rb(float distance, float rigid_body_radii, float rigid_body_Influence_radius)
    {
        if (distance > rigid_body_Influence_radius){return 0;}

        float border_offset = Mathf.Max(distance - rigid_body_radii, 0);
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        return 50 * Mathf.Pow((rigid_body_Influence_radius - border_offset), 0.5f);
        // return 100 * Mathf.Pow(1 - border_offset/Max_influence_radius, 2);
    }
}