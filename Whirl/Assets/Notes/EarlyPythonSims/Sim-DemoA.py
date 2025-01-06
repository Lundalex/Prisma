import pygame
import sys
import random
import numpy as np
import math
import random

# --- Simulation Parameters ---
GravityForce = 20
Circle_radii = 5
Max_influence_radius = 100
Framerate_max = 1000
Program_speed = 5
Particle_amount = 200
Target_density = 130
Pressure_multiplier = 2000
Wall_collision_damping_factor = 0.8
Smooth_Max = 150
Smooth_derivative_koefficient = 0.1
Look_ahead_factor = 1 / 60

Viscocity = 0.01

# Min_distance is the threshhold for the sort function to not include a particle in a edge chunk
Min_distance = math.sqrt(2)
Min_distance_optimisations = False

# --- Pygame settings ---
w, h = 1000, 500
resolution_x, resolution_y = 50, 25

Dev_density = False
Dev_density_update_frequency = 100
Background_color_factor = 0.5
Dev_chunks = False

# --- Automatic ---
sign = lambda a: 1 if a>0 else -1 if a<0 else 0
Chunks_amount_x = int(w / Max_influence_radius)
Chunks_amount_y = int(h / Max_influence_radius)
if Chunks_amount_x % 1 != 0 or Chunks_amount_y % 1 != 0:
    raise TypeError("⚠ Chunks_amount not even")
scale_x, scale_y = w / resolution_x, h / resolution_y
class world_class:
    def __init__(self, width, height):
        self.width = width
        self.height = height
        self.resolution_pixels = np.zeros((resolution_x, resolution_y))
world = world_class(w, h)
particles = []
frame_counter = 1
particle_chunks = np.empty((Chunks_amount_x, Chunks_amount_y), dtype=object)
for i in range(Chunks_amount_x):
    for j in range(Chunks_amount_y):
        particle_chunks[i, j] = set()

# --- Pygame setup ---
pygame.init()
screen = pygame.display.set_mode((world.width, world.height))
surface = pygame.Surface((world.width, world.height))
clock = pygame.time.Clock()
pygame.display.set_caption("Liquid Simulation")

# Create particles
class particle_class:
    def __init__(self, x, y):
        self.x = x
        self.y = y
        self.velocity_y = 0
        self.velocity_x = 0
        self.density = 0
        self.mass = 1
        self.predicted_x = 0
        self.predicted_x = 0
        
for x in range(Particle_amount):
    particles.append(particle_class(random.randint(50, world.width - 50), random.randint(50, world.height - 50)))

# Sorts particles into particle_chunks[x, y][]
def Sort_particles(particles):
    
    # Sort particles into particle_chunks[x, y][]
    # Also asign predicted positions
    for particle in particles:
        
        particle.predicted_x = particle.x + particle.velocity_x * Look_ahead_factor
        particle.predicted_y = particle.y + particle.velocity_y * Look_ahead_factor
        
        in_chunk_x = math.floor(particle.predicted_x / Max_influence_radius)
        in_chunk_y = math.floor(particle.predicted_y / Max_influence_radius)
        relative_position_x = (particle.predicted_x / Max_influence_radius) % 1
        relative_position_y = (particle.predicted_y / Max_influence_radius) % 1
        chunks_amount_x, chunks_amount_y = particle_chunks.shape
        
        for x in range(-1, 2):
            for y in range(-1, 2):
                if (-1 < (in_chunk_x + x) < chunks_amount_x) and (-1 < (in_chunk_y + y) < chunks_amount_y):
                    particle_chunks[in_chunk_x + x, in_chunk_y + y].add(particle)
        
        if not Min_distance_optimisations:
            continue
        # lower-left, lower-right, upper-left, upper-right. Only ONE of these cases are possible, thus elif
        if (math.sqrt(relative_position_x**2 + relative_position_y**2) > Min_distance) and (0 < in_chunk_x) and (0 < in_chunk_y):
            particle_chunks[in_chunk_x - 1, in_chunk_y - 1].remove(particle)
        if (math.sqrt((1 - relative_position_x)**2 + relative_position_y**2) > Min_distance) and (in_chunk_x < chunks_amount_x - 1) and (0 < in_chunk_y):
            particle_chunks[in_chunk_x + 1, in_chunk_y - 1].remove(particle)
        if (math.sqrt(relative_position_x**2 + (1 - relative_position_y)**2) > Min_distance) and (0 < in_chunk_x) and (in_chunk_y < chunks_amount_y - 1):
            particle_chunks[in_chunk_x - 1, in_chunk_y + 1].remove(particle)
        if (math.sqrt((1 - relative_position_x)**2 + (1 - relative_position_y)**2) > Min_distance) and (in_chunk_x < chunks_amount_x - 1) and (in_chunk_y < chunks_amount_y - 1):
            particle_chunks[in_chunk_x + 1, in_chunk_y + 1].remove(particle)
    
    return

# Finds the "influence" value at a specific (particle's) location
def Influence(particle):
    
    in_chunk_x = math.floor(particle.predicted_x / Max_influence_radius)
    in_chunk_y = math.floor(particle.predicted_y / Max_influence_radius)
    
    # Sum up each influence value * mass.
    # (distance = math.sqrt((particle.predicted_x - other_particle.predicted_x()**2 + (particle.predicted_y - other_particle.predicted_y)**2))
    tot_influence = sum(
        Smooth(math.sqrt((particle.predicted_x - other_particle.predicted_x)**2 + (particle.predicted_y - other_particle.predicted_y)**2)) * other_particle.mass
        for other_particle in particle_chunks[in_chunk_x, in_chunk_y]
    )
    
    # 255 is max for hex color input
    return tot_influence if type(tot_influence) != int else 0

def Precalculate_densities(particles):
    
    for particle in particles:
        particle.density = Influence(particle)
  
# Finds the "influence" value at a specific (pixel's) location
def Influence_pixels(x, y):
    
    # distance = math.sqrt((particle.x - x)**2 + (particle.y - y)**2)
    tot_influence = sum(
        Smooth(math.sqrt((particle.predicted_x - x)**2 + (particle.predicted_y - y)**2))
        for particle in particles
        if math.sqrt((particle.predicted_x - x)**2 + (particle.predicted_y - y)**2) <= Max_influence_radius
    )
    
    return 255 if tot_influence * Background_color_factor > 255 else tot_influence * Background_color_factor

# (Smoothing function) - return value increases as the distance (between two particles) decreases
def Smooth(distance):
    
    # Geogebra: https://www.geogebra.org/calculator/vwapudgf
    return Smooth_Max * math.exp(-Smooth_derivative_koefficient * distance)

# (Smoothing function derivative)
def Smooth_gradient(distance):
    
    # Geogebra: https://www.geogebra.org/calculator/vwapudgf
    return -Smooth_Max * Smooth_derivative_koefficient * math.exp(-Smooth_derivative_koefficient * distance)

def Density_to_pressure(density):
    density_error = density - Target_density
    pressure = density_error * Pressure_multiplier
    return pressure

def Shared_pressure(density_A, density_B):
    
    pressure_A = Density_to_pressure(density_A)
    pressure_B = Density_to_pressure(density_B)
    
    shared_pressure = (pressure_A + pressure_B) / 2
    
    return shared_pressure

def Pressure_force(particle):
    
    pressure_force_x = 0
    pressure_force_y = 0
    
    in_chunk_x = math.floor(particle.predicted_x / Max_influence_radius)
    in_chunk_y = math.floor(particle.predicted_y / Max_influence_radius)
    
    for other_particle in particle_chunks[in_chunk_x, in_chunk_y]:
        
        if other_particle == particle:
            continue
        
        relative_distance_x = other_particle.predicted_x - particle.predicted_x
        relative_distance_y = other_particle.predicted_y - particle.predicted_y
    
        distance = math.sqrt(relative_distance_x**2 + relative_distance_y**2)
            
        abs_influence_gradient = Smooth_gradient(distance)

        #                            ("(x/y)"_distance_vector_value)
        pressure_gradient_x = 0
        pressure_gradient_y = 0
        if distance != 0:
            pressure_gradient_x = (relative_distance_x / distance) * abs_influence_gradient
            pressure_gradient_y = (relative_distance_y / distance) * abs_influence_gradient
        else:
            pressure_gradient_x = random.choice([-1, 1]) * abs_influence_gradient
            pressure_gradient_y = random.choice([-1, 1]) * abs_influence_gradient
            
        # average_pressure between particle and other_particle
        average_pressure = Shared_pressure(particle.density, other_particle.density)
    
        # Density_to_pressure(particle.density) = pressure_tot
        # p(pos) = ∑_i (p_i * m / ρ_i * Smooth(pos - pos_i))
        pressure_force_x += average_pressure * pressure_gradient_x * other_particle.mass / other_particle.density
        pressure_force_y += average_pressure * pressure_gradient_y * other_particle.mass / other_particle.density
    
    return pressure_force_x, pressure_force_y

def Apply_viscocity_to_velocity(particle):
    
    in_chunk_x = math.floor(particle.predicted_x / Max_influence_radius)
    in_chunk_y = math.floor(particle.predicted_y / Max_influence_radius)
    
    tot_force_x = 0
    tot_force_y = 0
    tot_mass = 0
    
    for other_particle in particle_chunks[in_chunk_x, in_chunk_y]:
        
        if particle == other_particle:
            continue
        
        relative_distance_x = other_particle.predicted_x - particle.predicted_x
        relative_distance_y = other_particle.predicted_y - particle.predicted_y
    
        distance = math.sqrt(relative_distance_x**2 + relative_distance_y**2)
        
        abs_force = Smooth(distance)
        
        force_x = (relative_distance_x / distance) * abs_force
        force_y = (relative_distance_y / distance) * abs_force
        
        tot_force_x += force_x
        tot_force_y += force_y
        
        tot_mass += other_particle.mass
    
    if tot_mass == 0:
        tot_mass = 1
        
    average_viscocity_velocity_x = tot_force_x / tot_mass
    average_viscocity_velocity_y = tot_force_y / tot_mass
    
    new_velocity_x = ((1 - Viscocity) * particle.velocity_x + Viscocity * average_viscocity_velocity_x)
    new_velocity_y = ((1 - Viscocity) * particle.velocity_y + Viscocity * average_viscocity_velocity_y)
    
    return new_velocity_x, new_velocity_y
    
# Apply all physics
def Physics(particles, delta_time):
    
    for particle in particles:
        
        # Apply gravitional forces
        particle.velocity_y += GravityForce * delta_time
        
        # Add Pressure_force to velocity_"(x/y)"
        pressure_force_x, pressure_force_y = Pressure_force(particle)
        
        particle.velocity_x += pressure_force_x / particle.density
        particle.velocity_y += pressure_force_y / particle.density
        
        # Viscocity:
        particle.velocity_x, particle.velocity_y = Apply_viscocity_to_velocity(particle)
        
        # Increment x,y by velocity_"(x/y)" with respect to delta_time
        particle.x += particle.velocity_x * delta_time
        particle.y += particle.velocity_y * delta_time
        
        # Solid screen borders
        if particle.y > world.height - Circle_radii:
            particle.velocity_y = -abs(particle.velocity_y * Wall_collision_damping_factor)
            particle.y = world.height - Circle_radii
        
        elif particle.y < Circle_radii:
            particle.velocity_y = +abs(particle.velocity_y * Wall_collision_damping_factor)
            particle.y = Circle_radii

        if particle.x > world.width - Circle_radii:
            particle.velocity_x = -abs(particle.velocity_x * Wall_collision_damping_factor)
            particle.x = world.width - Circle_radii

        elif particle.x < Circle_radii:
            particle.velocity_x = +abs(particle.velocity_x * Wall_collision_damping_factor)
            particle.x = Circle_radii

# Render all elements to screen
def Render(particles, Framerate_max, resolution_pixels):
    
    # Get density for every screen pixel
    if Dev_density and 0 == frame_counter % Dev_density_update_frequency:
        for x in range(resolution_x):
            for y in range(resolution_y):
                resolution_pixels[x, y] = Influence_pixels(x * scale_x, y * scale_y)

    # Rectangle method - Draw density pixels
    if Dev_density:
        for x in range(resolution_x):
            for y in range(resolution_y):
                pygame.draw.rect(screen, (0, resolution_pixels[x, y], 0), (x * scale_x, y * scale_y, scale_x, scale_y))
                
    # Visualise active chunks
    if Dev_chunks:
        chunks_amount_x, chunks_amount_y = particle_chunks.shape
        for x in range(chunks_amount_x):
            for y in range(chunks_amount_y):
                if len(particle_chunks[x, y]) > 0:
                    resolution_pixels[x, y] = 255
                else:
                    resolution_pixels[x, y] = 50
    
   # Draw particles
    for particle in particles:
        particle.display_color = particle.density
        pygame.draw.circle(screen, (0, 0, min(math.floor(particle.display_color), 255)), (particle.x, particle.y), Circle_radii)
    
    # FPS counter
    fps = int(clock.get_fps())
    fps_text = font.render(f"FPS: {fps}", True, (0, 150, 0))
    screen.blit(fps_text, (10, 10))
    
    # Update the display
    pygame.display.flip()
    clock.tick(Framerate_max)

# Main game loop
running = True
start_time = pygame.time.get_ticks()
font = pygame.font.Font(None, 36)
clock = pygame.time.Clock()
while running:
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False
    # Clear the screen
    screen.fill((255, 255, 255))
    # Set ΔTime
    current_time = pygame.time.get_ticks()
    delta_time = (current_time - start_time) * Program_speed / 1000.0
    start_time = current_time
    frame_counter += 1
    
    # Reset particle_chunks tuple and populate with empty sets
    particle_chunks = np.empty((int(Chunks_amount_x), int(Chunks_amount_y)), dtype=object)
    for i in range(int(Chunks_amount_x)):
        for j in range(int(Chunks_amount_y)):
            particle_chunks[i, j] = set()
            
    Sort_particles(particles)
    
    Precalculate_densities(particles)
    
    Physics(particles, delta_time)
    
    Render(particles, Framerate_max, world.resolution_pixels)

# Quit Pygame
pygame.quit()
sys.exit()