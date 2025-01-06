import math

def dot_product(v1, v2):
    return sum((a*b) for a, b in zip(v1, v2))

def cross_product_z(v1, v2):
    return v1[0] * v2[1] - v1[1] * v2[0]

def vector_from_points(p1, p2):
    return (p2[0] - p1[0], p2[1] - p1[1])

def length_of_vector(vector):
    return math.sqrt(dot_product(vector, vector))

def signed_distance_point_to_line_segment(A, B, P):
    # Create vectors
    AB = vector_from_points(A, B)
    AP = vector_from_points(A, P)
    AB_length_squared = dot_product(AB, AB)  # AB squared

    # Project AP onto AB (scalar projection)
    AP_dot_AB = dot_product(AP, AB)
    t = AP_dot_AB / AB_length_squared

    # Handle cases where projection is not on the line segment
    if t < 0.0:
        t = 0.0
    elif t > 1.0:
        t = 1.0

    # Find closest point on line segment to P
    closest_point = (A[0] + t * AB[0], A[1] + t * AB[1])

    # Calculate distance vector
    distance_vector = vector_from_points(P, closest_point)

    # Calculate and return signed distance from P to closest point
    distance = length_of_vector(distance_vector)
    sign = math.copysign(1, cross_product_z(AB, AP))

    return distance * sign

# Example
A = (2, 1)
B = (3, 4)
P = (3, 1)
distance = signed_distance_point_to_line_segment(A, B, P)
print("Signed distance from P to line segment AB:", distance)
