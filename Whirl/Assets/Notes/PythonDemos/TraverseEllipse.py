import math

def traverse_line(lineParameters):
    return True


def TraverseEllipse(a, b, angleIncrement):
    theta = 0
    twoPi = 2 * math.pi
    points = []

    while theta < twoPi:
        x = a * math.cos(theta)
        y = b * math.sin(theta)
        points.append((x, y))
        theta += angleIncrement

    # Handle the final step to close the ellipse
    x = a * math.cos(0)
    y = b * math.sin(0)
    points.append((x, y))

    return points

# a and b define the width and height of the eclipse, or circle if a = b
a = 5
b = 3
# angleIncrement = max(MaxInfluenceRadius, SafeValue)
# where SafeValue makes sure the algorithm clips all chunks
# if abs(closest dst to chunk) - SafeValue/2, the chunk is added to clippedChunksAppendBuffer[]
# The variable SafeValue is intended to be adjusted for performance.
angleIncrement = 0.1

pointsOnEllipse = TraverseEllipse(a, b, angleIncrement)
for point in pointsOnEllipse:
    print(point)