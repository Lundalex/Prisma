using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Quaternion = UnityEngine.Quaternion;
public class Simulation_SingleCore : MonoBehaviour
{
    public GameObject particle_prefab;
    private List<GameObject> particles = new();
    // Creating a 2D matrix where each element is a list of integers
    private List<List<HashSet<int>>> particle_chunks = new();
    private GameObject simulation_boundary;

    public int particles_num = 600;
    public int border_width = 26;
    public int border_height = 13;
    public float Gravity_force = 3f;
    public float Max_influence_radius = 1;
    public int Framerate_max = 1000;
    public float Program_speed = 2f;
    public float Target_density = 10f;
    public float Pressure_multiplier = 70f;
    public float Collision_damping_factor = 0.4f;
    public float Smooth_Max = 5f;
    public float Smooth_derivative_koefficient = 2.5f;
    public float Look_ahead_factor = 0.02f;
    public int Chunk_amount_multiplier = 2;
    public float border_thickness = 0.2f;
    public float Viscocity = 0.2f;
    private int Chunk_amount_x = 0;
    private int Chunk_amount_y = 0;
    private float Chunk_amount_multiplier_squared = 0;

    private Vector2[] position;
    private Vector2[] velocity;
    // Predicted_velocities
    private Vector2[] p_position;
    private List<float> density;
    private float delta_time;
    private (uint, uint)[] particles_w_chunks;

    void Start()
    {
        // Create border
        Create_simulation_boundary();

        // initialize particle property arrays
        position = new Vector2[particles_num];
        velocity = new Vector2[particles_num];
        p_position = new Vector2[particles_num];
        density = new List<float>(particles_num);

        particles_w_chunks = new (uint, uint)[particles_num];


        Chunk_amount_x = (int)(border_width * Chunk_amount_multiplier / Max_influence_radius);
        Chunk_amount_y = (int)(border_height * Chunk_amount_multiplier / Max_influence_radius);

        Chunk_amount_multiplier_squared = Chunk_amount_multiplier * Chunk_amount_multiplier;
        // Initialize particle_chunks
        particle_chunks = new List<List<HashSet<int>>>();

        for (int depth1 = 0; depth1 < Chunk_amount_x; depth1++)
        {
            List<HashSet<int>> innerList = new();
            
            for (int depth2 = 0; depth2 < Chunk_amount_y; depth2++)
            {
                innerList.Add(new HashSet<int>());
            }

            particle_chunks.Add(innerList);
        }

        density = new List<float>(particles_num);
        for (int i = 0; i < particles_num; i++)
        {
            density.Add(0.0f); // Or any default value you prefer
        }

        // Create particles
        for (int i = 0; i < particles_num; i++)
        {
            Create_particle();
        }

        // Assign random positions
        for (int i = 0; i < particles_num; i++)
        {
            position[i] = Random_particle_position();
        }

        SortParticleIndices();
    }

    void Update()
    {
        delta_time = Time.deltaTime * Program_speed;

        Sort_particles();

        Precalculate_densities();

        Physics();

        // Render
        for (int i = 0; i < particles_num; i++)
        {
            particles[i].transform.position = new Vector3(position[i].x, position[i].y, 0);
        }
    }

    void SortParticleIndices()
    {
        for (int i = 0; i < particles_num; i++)
        {
            uint particle_index = particles_w_chunks[i].Item1;
            Vector2 pos = position[particle_index];

            (uint, uint) chunk = ((uint)(pos.x * Chunk_amount_multiplier / Max_influence_radius), (uint)(pos.y * Chunk_amount_multiplier / Max_influence_radius));

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

    void Sort_particles()
    {
        for(int i = 0; i < particles_num; i++)
        {
            // Set predicted velocities
            p_position[i] = position[i] + velocity[i] * Look_ahead_factor;

            int inChunkX = (int)(p_position[i].x * Chunk_amount_multiplier / Max_influence_radius);
            int inChunkY = (int)(p_position[i].y * Chunk_amount_multiplier / Max_influence_radius);
            float relative_position_x = p_position[i].x * Chunk_amount_multiplier / Max_influence_radius % 1;
            float relative_position_y = p_position[i].y * Chunk_amount_multiplier / Max_influence_radius % 1;

            for (int x = -Chunk_amount_multiplier; x <= Chunk_amount_multiplier; x++)
            {
                for (int y = -Chunk_amount_multiplier; y <= Chunk_amount_multiplier; y++)
                {
                    // current chunk
                    int cur_chunk_x = inChunkX + x;
                    int cur_chunk_y = inChunkY + y;

                    if (cur_chunk_x >= 0 && cur_chunk_x < Chunk_amount_x && cur_chunk_y >= 0 && cur_chunk_y < Chunk_amount_y)
                    {
                        if (Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y-relative_position_y)*(y-relative_position_y) ||
                            Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y-relative_position_y)*(y-relative_position_y)||
                            Chunk_amount_multiplier_squared > (x-relative_position_x)*(x-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y) ||
                            Chunk_amount_multiplier_squared > (x+1-relative_position_x)*(x+1-relative_position_x)+(y+1-relative_position_y)*(y+1-relative_position_y))
                        {
                            particle_chunks[cur_chunk_x][cur_chunk_y].Add(i);
                        }
                    }
                }
            }
        }
    }

    float Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        float totInfluence = particle_chunks[in_chunk_x][in_chunk_y]
            .Sum(other_particle_index =>
                Smooth(Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[other_particle_index].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[other_particle_index].y, 2))));
        return totInfluence;
    }

    void Precalculate_densities()
    {
        for (int i = 0; i < particles_num; i++)
        {
            density[i] = Influence(i);
        }
    }

    float Smooth(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        return Smooth_Max * Mathf.Exp(-Smooth_derivative_koefficient * distance);
    }

    float Smooth_der(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/vwapudgf
        return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
    }

    float Density_to_pressure(float density)
    {
        float density_error = density - Target_density;
        float pressure = density_error * Pressure_multiplier;
        return pressure;
    }

    float Shared_pressure(float density_A, float density_B)
    {
        float pressure_A = Density_to_pressure(density_A);
        float pressure_B = Density_to_pressure(density_B);

        float Shared_value = (pressure_A + pressure_B) / 2;

        return Shared_value;
    }

    Vector2 Pressure_force(int particle_index)
    {

        Vector2 pressure_force = new(0.0f, 0.0f);

        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);

        foreach (int other_particle_index in particle_chunks[in_chunk_x][in_chunk_y])
        {
            if (other_particle_index == particle_index)
            {
                continue;
            }

            UnityEngine.Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            float abs_pressure_gradient = Smooth_der(distance);

            UnityEngine.Vector2 pressure_gradient = new(0.0f, 0.0f);

            if (distance != 0)
            {
                pressure_gradient = relative_distance.normalized * abs_pressure_gradient;
            }
            else
            {
                Vector2 randomNormalizedVector = new(UnityEngine.Random.onUnitSphere.x, UnityEngine.Random.onUnitSphere.y);

                // * 0.1f to decrease pressure forces (for "corner particles")
                pressure_gradient = 0.1f * abs_pressure_gradient * randomNormalizedVector;
            }

            float avg_pressure = Shared_pressure(density[particle_index], density[other_particle_index]);

            // p(pos) = ∑_i (p_i * m / ρ_i * Smooth(pos - pos_i))
            pressure_force += avg_pressure * pressure_gradient / density[other_particle_index];

        }

        return -pressure_force;

    }

    Vector2 Apply_viscocity_to_velocity(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(p_position[particle_index].x * Chunk_amount_multiplier / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(p_position[particle_index].y * Chunk_amount_multiplier / Max_influence_radius);
        int tot_mass = 0;

        Vector2 tot_force = new(0.0f, 0.0f);

        foreach (int other_particle_index in particle_chunks[in_chunk_x][in_chunk_y])
        {
            tot_mass += 1;

            if (other_particle_index == particle_index)
            {
                continue;
            }

            Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            if (distance == 0)
                continue;
            
            float abs_force = Smooth(distance);

            Vector2 force = new(relative_distance.normalized.x * abs_force, relative_distance.normalized.y * abs_force);

            tot_force += force;
        }

        if (tot_mass == 0)
        {
            tot_mass = 1;
        }

        Vector2 average_viscocity_velocity_x = tot_force / tot_mass;

        Vector2 new_velocity = (1 - Viscocity * delta_time) * velocity[particle_index] + Viscocity * delta_time * average_viscocity_velocity_x;

        return new_velocity;
    }

    void Physics()
    {
        for (int i = 0; i < particles_num; i++)
        {
            velocity[i].y -= Gravity_force * delta_time;

            Vector2 pressure_force = Pressure_force(i);
            
            velocity[i] += pressure_force * delta_time / density[i];

            velocity[i] = Apply_viscocity_to_velocity(i);

            position[i] += velocity[i] * delta_time;

            if (position[i].y > border_height - border_thickness)
            {
                velocity[i].y = -Mathf.Abs(velocity[i].y * Collision_damping_factor);
                position[i].y = border_height - border_thickness;
            }
            if (position[i].y < border_thickness)
            {
                velocity[i].y = +Mathf.Abs(velocity[i].y * Collision_damping_factor);
                position[i].y = border_thickness;
            }
            if (position[i].x > border_width - border_thickness)
            {
                velocity[i].x = -Mathf.Abs(velocity[i].x * Collision_damping_factor);
                position[i].x = border_width - border_thickness;
            }
            if (position[i].x < border_thickness)
            {
                velocity[i].x = +Mathf.Abs(velocity[i].x * Collision_damping_factor);
                position[i].x = border_thickness;
            }
        }
    }

    void Create_particle()
    {
        GameObject particle = Instantiate(particle_prefab, Random_particle_position(), Quaternion.identity);
        particle.transform.parent = transform;
        particles.Add(particle);
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


    Vector2 Random_particle_position()
    {
        float x = UnityEngine.Random.Range(1, border_width-1);
        float y = UnityEngine.Random.Range(1, border_height-1);
        return new Vector2(x, y);
    }
}
