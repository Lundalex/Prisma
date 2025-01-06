using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using System;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct Populate_particle_chunks_job : IJob {
    [ReadOnly] public NativeArray<Vector2> velocity;
    [ReadOnly] public NativeArray<Vector2> position;
    public NativeArray<int> particle_chunks;
    public NativeArray<Vector2> p_position;
    public int Max_influence_radius;
    public int Chunk_amount_x;
    public int Chunk_amount_y;
    public int Chunk_capacity;
    public int particles_num;
    public float Look_ahead_factor;
    public void Execute() {

        for (int i = 0; i < particles_num; i++)
        {
            p_position[i] = position[i] + velocity[i] * Look_ahead_factor;

            int in_chunk_x = (int)Math.Floor(p_position[i].x / Max_influence_radius);
            int in_chunk_y = (int)Math.Floor(p_position[i].y / Max_influence_radius);
            float relative_position_x = p_position[i].x / Max_influence_radius % 1;
            float relative_position_y = p_position[i].y / Max_influence_radius % 1;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
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
}

[BurstCompile]
public struct Populate_lg_particle_chunks_job : IJob {
    [ReadOnly] public NativeArray<Vector2> position;
    public NativeArray<int> lg_particle_chunks;
    public int Max_influence_radius;
    public int Lg_chunk_amount_y;
    public int Lg_chunk_capacity;
    public int particles_num;
    public void Execute() {

        for (int i = 0; i < particles_num; i++)
        {
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
}

[BurstCompile]
public struct Calculate_density_job : IJobParallelFor {
    public int Max_influence_radius;
    public int flat_particle_chunks_tot_indexes;
    public float Smooth_Max;
    public float Smooth_derivative_koefficient;
    public int Chunk_amount_y;
    public int Chunk_amount_x;
    public int Chunk_capacity;
    [ReadOnly] public NativeArray<Vector2> p_position;
    [ReadOnly] public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<int> particle_chunks;
    public NativeArray<float> density;
    public NativeArray<float> near_density;
    public void Execute(int i) {

        (density[i], near_density[i]) = Influence(i);
    }

    (float, float) Influence(int particle_index)
    {
        int in_chunk_x = (int)Math.Floor(position[particle_index].x / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y / Max_influence_radius);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        float totInfluence = 0.0f;
        float totNearInfluence = 0.0f;

        for (int i = start_i; i < end_i; i++)
        {
            if (particle_chunks[i] == -1){break;}

            float distance = Mathf.Sqrt(Mathf.Pow(p_position[particle_index].x - p_position[particle_chunks[i]].x, 2) + Mathf.Pow(p_position[particle_index].y - p_position[particle_chunks[i]].y, 2));

            totInfluence += Smooth(distance);
            totNearInfluence += Smooth_near(distance);
        }

        return (totInfluence, totNearInfluence);
    }

    float Smooth(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        return Mathf.Pow(1 - distance/Max_influence_radius, 2);
    }

    float Smooth_near(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        // return Smooth_Max * Mathf.Exp(2 * -Smooth_derivative_koefficient * distance);
        return Mathf.Pow(1 - distance/Max_influence_radius, 3);
    }
}

[BurstCompile]
public struct Calculate_physics_job : IJobParallelFor {

    public float Gravity_force;
    public float delta_time;
    public int border_height;
    public int border_width;
    public float border_thickness;
    public float Collision_damping_factor;
    public float Smooth_Max;
    public float Smooth_derivative_koefficient;
    public float Target_density;
    public float Pressure_multiplier;
    public int Max_influence_radius;
    public float Viscocity;
    public float Max_interaction_radius;
    public int Chunk_amount_y;
    public int Chunk_capacity;
    public float Interaction_power;
    public float Max_velocity;
    public float Near_density_multiplier;
    public Vector2 mouse_position;
    public bool left_mouse_button_down;
    public bool right_mouse_button_down;
    public NativeArray<Vector2> velocity;
    public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<Vector2> p_position;
    [ReadOnly] public NativeArray<float> density;
    [ReadOnly] public NativeArray<float> near_density;
    [ReadOnly] public NativeArray<Vector2> last_velocity;
    [ReadOnly] public NativeArray<int> particle_chunks;

    public void Execute(int i) {

        position[i] = new(Mathf.Max(Mathf.Min(position[i].x, border_width-0.1f), 0), Mathf.Max(Mathf.Min(position[i].y, border_height-0.1f), 0));

        velocity[i] = new Vector2(velocity[i].x, velocity[i].y - Gravity_force * delta_time);

        Vector2 pressure_force = Pressure_force(i);
        Vector2 interaction_force = Interaction_force(i);
        Vector2 viscocity_force = Viscocity_force(i);
        //                                                                                 / density[i] or constant?
        velocity[i] += (pressure_force + interaction_force + viscocity_force) * delta_time / 3;


        position[i] += velocity[i] * delta_time;

        if (position[i].y > border_height - border_thickness)
        {
            velocity[i] = new Vector2(velocity[i].x, -Mathf.Abs(velocity[i].y * Collision_damping_factor));
            position[i] = new Vector2(position[i].x, border_height - border_thickness);
        }
        if (position[i].y < border_thickness)
        {
            velocity[i] = new Vector2(velocity[i].x, +Mathf.Abs(velocity[i].y * Collision_damping_factor));
            position[i] = new Vector2(position[i].x, border_thickness);
        }
        if (position[i].x > border_width - border_thickness)
        {
            velocity[i] = new Vector2(-Mathf.Abs(velocity[i].x * Collision_damping_factor), velocity[i].y);
            position[i] = new Vector2(border_width - border_thickness, position[i].y);
        }
        if (position[i].x < border_thickness)
        {
            velocity[i] = new Vector2(+Mathf.Abs(velocity[i].x * Collision_damping_factor), velocity[i].y);
            position[i] = new Vector2(border_thickness, position[i].y);
        }
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

        int in_chunk_x = (int)Math.Floor(position[particle_index].x / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y / Max_influence_radius);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        for (int i = start_i; i < end_i; i++)
        {
            int other_particle_index = particle_chunks[i];

            if (other_particle_index == particle_index || other_particle_index == -1)
            {
                continue;
            }

            Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            if (distance > Max_influence_radius){continue;}

            float abs_pressure_gradient = Smooth_der(distance);
            float abs_near_pressure_gradient = Smooth_near_der(distance);

            Vector2 pressure_gradient = new(0.0f, 0.0f);
            Vector2 near_pressure_gradient = new(0.0f, 0.0f);

            if (distance == 0)
            {
                Vector2 randomNormalizedVector = new(particle_index % 0.2842f, particle_index % 0.1827f);

                pressure_gradient = 0.05f * abs_pressure_gradient * randomNormalizedVector;
                near_pressure_gradient = 0.05f * abs_near_pressure_gradient * randomNormalizedVector;
            }
            else
            {
                pressure_gradient = relative_distance.normalized * abs_pressure_gradient;
                near_pressure_gradient = relative_distance.normalized * abs_near_pressure_gradient;
            }

            float density_A = density[particle_index];
            float density_B = density[other_particle_index];

            float avg_pressure = Shared_pressure(density_A, density_B);

            float near_pressure_A = near_density[particle_index] * Near_density_multiplier;
            float near_pressure_B = near_density[other_particle_index] * Near_density_multiplier;

            float avg_near_pressure = (near_pressure_A + near_pressure_B) / 2;

            // p(pos) = ∑_i (p_i * m / ρ_avg * Smooth(pos - pos_i))
            pressure_force += (avg_pressure * pressure_gradient + avg_near_pressure * near_pressure_gradient) / ((density_A + density_B) / 2);

        }

        return -pressure_force;
    }

    Vector2 Viscocity_force(int particle_index)
    {
        Vector2 viscosityForce = new(0.0f, 0.0f);

        int in_chunk_x = (int)Math.Floor(position[particle_index].x / Max_influence_radius);
        int in_chunk_y = (int)Math.Floor(position[particle_index].y / Max_influence_radius);

        int start_i = in_chunk_x * Chunk_amount_y * Chunk_capacity + in_chunk_y * Chunk_capacity;
        int end_i = start_i + Chunk_capacity;

        for (int i = start_i; i < end_i; i++)
        {
            int other_particle_index = particle_chunks[i];

            if (other_particle_index == particle_index || other_particle_index == -1)
            {
                continue;
            }

            Vector2 relative_distance = p_position[particle_index] - p_position[other_particle_index];

            float distance = relative_distance.magnitude;

            if (distance > Max_influence_radius){continue;}

            float abs_viscocity_gradient = Smooth_viscocity_laplacian(distance);

            Vector2 viscocity_gradient = new(0.0f, 0.0f);

            if (distance == 0)
            {
                viscocity_gradient = new(0.0f, 0.0f);
            }
            else
            {
                viscocity_gradient = relative_distance.normalized * abs_viscocity_gradient;
            }

            Vector2 velocity_A = last_velocity[particle_index];
            Vector2 velocity_B = last_velocity[other_particle_index];

            float diff_velocity = velocity_A.magnitude - velocity_B.magnitude;

            float density_A = density[particle_index];
            float density_B = density[other_particle_index];

            // p(pos) = ∑_i (m * v_diff / ρ_avg * Smooth_viscocity(pos - pos_i))
            viscosityForce += (diff_velocity * viscocity_gradient) / ((density_A + density_B) / 2);

        }
        // Viscocity_multiplier*
        return Viscocity * viscosityForce;
    }

    Vector2 Interaction_force(int particle_index)
    {
        if (!left_mouse_button_down && !right_mouse_button_down){return new(0.0f,0.0f);}
        int left_right_direction = 0;
        if (left_mouse_button_down){left_right_direction = -1;}
        else{if (right_mouse_button_down){left_right_direction = 1;}}

        Vector2 relative_distance = p_position[particle_index] - mouse_position;

        float distance = relative_distance.magnitude;

        if (distance > Max_interaction_radius){return new(0.0f, 0.0f);}

        float abs_interaction_gradient = Interaction_influence(distance);

        if (distance > Max_interaction_radius){return new(0.0f, 0.0f);}
        else
        {
            if (distance == 0){return new(0.0f, 0.0f);}

            Vector2 interaction_gradient = relative_distance.normalized * abs_interaction_gradient;

            return interaction_gradient * Interaction_power * left_right_direction;
        }
    }

    float Interaction_influence(float distance)
    {
        if (distance == Max_interaction_radius){return 0.01f;}
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        return Mathf.Pow(Max_interaction_radius - distance, 0.7f);
    }

    float Smooth(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        // return Smooth_Max * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return Mathf.Pow(1 - distance/Max_influence_radius, 2);
    }

    float Smooth_der(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        // return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return -2 * (1 - distance/Max_influence_radius) / Max_influence_radius;
    }

    float Smooth_near(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        // return Smooth_Max * Mathf.Exp(2 * -Smooth_derivative_koefficient * distance);
        return Mathf.Pow(1 - distance/Max_influence_radius, 3);
    }

    float Smooth_near_der(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        // return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return -3 * Mathf.Pow(1 - distance/Max_influence_radius, 2) / Max_influence_radius;
    }

    float Smooth_viscocity_laplacian(float distance)
    {
        // Geogebra: https://www.geogebra.org/calculator/bsyseckq
        if (distance > Max_influence_radius){return 0;}
        // return -Smooth_Max * Smooth_derivative_koefficient * Mathf.Exp(-Smooth_derivative_koefficient * distance);
        return 45 / (3.14f * Mathf.Pow(Max_influence_radius, 6)) * (Max_influence_radius - distance);
    }
}

[BurstCompile]
public struct Resolve_eventual_rb_particle_collisions_job : IJobParallelFor {
    public float Gravity_force;
    public float delta_time;
    public int Lg_chunk_dims;
    public int Lg_chunk_amount_x;
    public int Lg_chunk_amount_y;
    public int Lg_chunk_capacity;
    public float Rb_Elasticity;
    public float Collision_damping_factor;
    public float border_height;
    public float border_width;
    public float border_thickness;
    [ReadOnly] public NativeArray<float> rb_radii;
    [ReadOnly] public NativeArray<float> rb_mass;
    [ReadOnly] public NativeArray<int> lg_particle_chunks;
    [ReadOnly] public NativeArray<Vector2> position;
    [ReadOnly] public NativeArray<Vector2> velocity;
    public NativeArray<Vector2> rb_velocity;
    public NativeArray<Vector2> rb_position;
    public NativeArray<Vector3_20x_array> position_data;
    public NativeArray<Vector3_20x_array> velocity_delta_data;
    public void Execute(int rb_i) {

        int free_index = 0;

        rb_velocity[rb_i] = new(rb_velocity[rb_i].x, rb_velocity[rb_i].y - Gravity_force * delta_time);
        rb_position[rb_i] = new(rb_position[rb_i].x + rb_velocity[rb_i].x * delta_time, rb_position[rb_i].y + rb_velocity[rb_i].y * delta_time);

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

                        rb_velocity[rb_i] -= center_impulse / rb_mass[rb_i];

                        int v_p = particle_index;
                        float v_x = rb_position[rb_i].x + relative_collision_position.x;
                        float v_y = rb_position[rb_i].y + relative_collision_position.y;
                        Vector3 v = new(v_p,v_x,v_y);
                        switch (free_index)
                        {
                            case 0: position_data[rb_i] = new Vector3_20x_array{s0 = v, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 1: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = v, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 2: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = v, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 3: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = v, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 4: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = v, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 5: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = v, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 6: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = v, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 7: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = v, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 8: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = v, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 9: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = v, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 10: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = v, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 11: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = v, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 12: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = v, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 13: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = v, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 14: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = v, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 15: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = v, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 16: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = v, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 17: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = v, s18 = position_data[rb_i].s18, s19 = position_data[rb_i].s19}; break;
                            case 18: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = v, s19 = position_data[rb_i].s19}; break;
                            case 19: position_data[rb_i] = new Vector3_20x_array{s0 = position_data[rb_i].s0, s1 = position_data[rb_i].s1, s2 = position_data[rb_i].s2, s3 = position_data[rb_i].s3, s4 = position_data[rb_i].s4, s5 = position_data[rb_i].s5, s6 = position_data[rb_i].s6, s7 = position_data[rb_i].s7, s8 = position_data[rb_i].s8, s9 = position_data[rb_i].s9, s10 = position_data[rb_i].s10, s11 = position_data[rb_i].s11, s12 = position_data[rb_i].s12, s13 = position_data[rb_i].s13, s14 = position_data[rb_i].s14, s15 = position_data[rb_i].s15, s16 = position_data[rb_i].s16, s17 = position_data[rb_i].s17, s18 = position_data[rb_i].s18, s19 = v}; break;
                        }

                        v_p = particle_index;
                        v_x = exchanged_momentum.x;
                        v_y = exchanged_momentum.y;
                        v = new(v_p,v_x,v_y);
                        switch (free_index)
                        {
                            case 0: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = v, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 1: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = v, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 2: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = v, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 3: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = v, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 4: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = v, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 5: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = v, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 6: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = v, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 7: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = v, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 8: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = v, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 9: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = v, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 10: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = v, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 11: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = v, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 12: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = v, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 13: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = v, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 14: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = v, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 15: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = v, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 16: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = v, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 17: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = v, s18 = velocity_delta_data[rb_i].s18, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 18: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = v, s19 = velocity_delta_data[rb_i].s19}; break;
                            case 19: velocity_delta_data[rb_i] = new Vector3_20x_array{s0 = velocity_delta_data[rb_i].s0, s1 = velocity_delta_data[rb_i].s1, s2 = velocity_delta_data[rb_i].s2, s3 = velocity_delta_data[rb_i].s3, s4 = velocity_delta_data[rb_i].s4, s5 = velocity_delta_data[rb_i].s5, s6 = velocity_delta_data[rb_i].s6, s7 = velocity_delta_data[rb_i].s7, s8 = velocity_delta_data[rb_i].s8, s9 = velocity_delta_data[rb_i].s9, s10 = velocity_delta_data[rb_i].s10, s11 = velocity_delta_data[rb_i].s11, s12 = velocity_delta_data[rb_i].s12, s13 = velocity_delta_data[rb_i].s13, s14 = velocity_delta_data[rb_i].s14, s15 = velocity_delta_data[rb_i].s15, s16 = velocity_delta_data[rb_i].s16, s17 = velocity_delta_data[rb_i].s17, s18 = velocity_delta_data[rb_i].s18, s19 = v}; break;
                        }
                        if (free_index == 19)
                        {
                            Debug.Log("indeces exceeded for rb-particle collisions");
                        }
    
                        free_index += 1;
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

[BurstCompile]
public struct Resolve_eventual_rb_rb_collisions_job : IJobParallelFor {

    public float Rb_Elasticity;
    public int rigid_bodies_num;
    [ReadOnly] public NativeArray<float> rb_radii;
    [ReadOnly] public NativeArray<float> rb_mass;
    [ReadOnly] public NativeArray<Vector2> copy_rb_velocity;
    [ReadOnly] public NativeArray<Vector2> copy_rb_position;
    public NativeArray<Vector2> rb_velocity;
    public NativeArray<Vector2> rb_position;
    public void Execute(int rb_i) {

        for (int other_rb_index = 0; other_rb_index < rigid_bodies_num; other_rb_index++)
        {
            if (other_rb_index == rb_i) {continue;}

            Vector2 distance = copy_rb_position[other_rb_index] - rb_position[rb_i];

            float abs_distance = distance.magnitude;

            if (abs_distance >= (rb_radii[other_rb_index]-0.37f) + (rb_radii[rb_i]-0.37f)){continue;}

            Vector2 velocity_diff = copy_rb_velocity[other_rb_index] - rb_velocity[rb_i];

            Vector2 norm_distance = distance.normalized;

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

            rb_velocity[rb_i] -= center_impulse / (2 * rb_mass[rb_i]);

            float overlap_distance = (rb_radii[other_rb_index]-0.37f) + (rb_radii[rb_i]-0.37f) - abs_distance;

            rb_position[rb_i] -= overlap_distance * norm_distance / 2;
        }
    }
}



// (index_value, vector.x, vector.y) (s0 -> s19)
public struct Vector3_20x_array
{
    public Vector3 s0;
    public Vector3 s1;
    public Vector3 s2;
    public Vector3 s3;
    public Vector3 s4;
    public Vector3 s5;
    public Vector3 s6;
    public Vector3 s7;
    public Vector3 s8;
    public Vector3 s9;
    public Vector3 s10;
    public Vector3 s11;
    public Vector3 s12;
    public Vector3 s13;
    public Vector3 s14;
    public Vector3 s15;
    public Vector3 s16;
    public Vector3 s17;
    public Vector3 s18;
    public Vector3 s19;
    public static Vector3_20x_array CreateInitialized()
    {
        Vector3_20x_array array = new Vector3_20x_array();
        array.s0 = new Vector3(-1, 0f, 0f);
        array.s1 = new Vector3(-1, 0f, 0f);
        array.s2 = new Vector3(-1, 0f, 0f);
        array.s3 = new Vector3(-1, 0f, 0f);
        array.s4 = new Vector3(-1, 0f, 0f);
        array.s5 = new Vector3(-1, 0f, 0f);
        array.s6 = new Vector3(-1, 0f, 0f);
        array.s7 = new Vector3(-1, 0f, 0f);
        array.s8 = new Vector3(-1, 0f, 0f);
        array.s9 = new Vector3(-1, 0f, 0f);
        array.s10 = new Vector3(-1, 0f, 0f);
        array.s11 = new Vector3(-1, 0f, 0f);
        array.s12 = new Vector3(-1, 0f, 0f);
        array.s13 = new Vector3(-1, 0f, 0f);
        array.s14 = new Vector3(-1, 0f, 0f);
        array.s15 = new Vector3(-1, 0f, 0f);
        array.s16 = new Vector3(-1, 0f, 0f);
        array.s17 = new Vector3(-1, 0f, 0f);
        array.s18 = new Vector3(-1, 0f, 0f);
        array.s19 = new Vector3(-1, 0f, 0f);

        return array;
    }
    public void SetVector(int index, Vector3 value)
    {
        switch (index)
        {
            case 0: s0 = value; break;
            case 1: s1 = value; break;
            case 2: s2 = value; break;
            case 3: s3 = value; break;
            case 4: s4 = value; break;
            case 5: s5 = value; break;
            case 6: s6 = value; break;
            case 7: s7 = value; break;
            case 8: s8 = value; break;
            case 9: s9 = value; break;
            case 10: s10 = value; break;
            case 11: s11 = value; break;
            case 12: s12 = value; break;
            case 13: s13 = value; break;
            case 14: s14 = value; break;
            case 15: s15 = value; break;
            case 16: s16 = value; break;
            case 17: s17 = value; break;
            case 18: s18 = value; break;
            case 19: s19 = value; break;
        }
    }
}