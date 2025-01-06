import numpy as np

# Dummy structures and data
class Particle:
    def __init__(self, position, order):
        self.Position = np.array(position)
        self.POrder = order

class Spring:
    def __init__(self, pLinkedA, pLinkedB):
        self.PLinkedA = pLinkedA
        self.PLinkedB = pLinkedB

# Example initialization
ParticleSpringsCombinedHalfLength = 5
FrameBufferCycle = True  # Toggle to simulate the frame buffer cycle
MaxInfluenceRadiusSqr = 9.0  # Max radius squared
InvMaxInfluenceRadius = 1 / 3.0  # Inverse of max radius for chunk calculation
ChunkNumW = 10  # Width of the chunk grid
ChunkSizes = np.random.randint(1, 5, size=(ChunkNumW**2))  # Dummy chunk sizes

# Initialize dummy particles and springs
Particles = [Particle((np.random.rand()*10, np.random.rand()*10), i) for i in range(10)]
ParticleSpringsCombined = [Spring(np.random.randint(0, 10), np.random.randint(0, 10)) for _ in range(10)]

def ValidChunk(x, y):
    return 0 <= x < ChunkNumW and 0 <= y < ChunkNumW

def TransferAllSpringData(id_x):
    i = id_x if FrameBufferCycle else id_x + ParticleSpringsCombinedHalfLength
    
    lastSpring_i = ParticleSpringsCombined[i]
    PData_A = Particles[lastSpring_i.PLinkedA]
    PData_B = Particles[lastSpring_i.PLinkedB]
    
    PosDiff = PData_A.Position - PData_B.Position
    absPosDiffSqr = np.dot(PosDiff, PosDiff)
    
    if absPosDiffSqr <= MaxInfluenceRadiusSqr:
        newChunkX_A = int(PData_A.Position[0] * InvMaxInfluenceRadius)
        newChunkY_A = int(PData_A.Position[1] * InvMaxInfluenceRadius)
        newChunkX_B = int(PData_B.Position[0] * InvMaxInfluenceRadius)
        newChunkY_B = int(PData_B.Position[1] * InvMaxInfluenceRadius)
        
        localSpringBCapacityOrder = 0
        inRangeAB = False
        for y in range(-1, 2):
            curChunkY = newChunkY_B + y
            for x in range(-1, 2):
                curChunkX = newChunkX_B + x
                if not ValidChunk(curChunkX, curChunkY):
                    continue
                if curChunkX == newChunkX_A and curChunkY == newChunkY_A:
                    inRangeAB = True
                    break
                curChunkKey = curChunkY * ChunkNumW + curChunkX
                localSpringBCapacityOrder += ChunkSizes[curChunkKey]
        
        if not inRangeAB:
            return  # Spring data is not transferred
        
        # Continue with data transfer logic as needed...
        print(f"Data transferred for spring index {i}")

# Simulate the shader execution for a single thread
for i in range(500000):
    for j in range(len(Particles)):
        TransferAllSpringData(j)  # Example thread ID x
