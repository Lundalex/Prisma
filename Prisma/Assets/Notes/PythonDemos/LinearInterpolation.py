# numpy used for vector operations
import numpy as np

def linear_interpolate_corrected(posA, posB, valA, valB, MSvalMin):
    t = (MSvalMin - valA) / (valB - valA)
    return np.array(posA) + t * (np.array(posB) - np.array(posA))

# Test
posA = [1, 1]
posB = [4, 4]
valA = 10
valB = 20
MSvalMin = 11

print(linear_interpolate_corrected(posA, posB, valA, valB, MSvalMin))