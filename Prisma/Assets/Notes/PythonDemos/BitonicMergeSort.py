from math import log2, ceil

# This originates from the visual representation of the alternative bitonic mergesort algorithm
# https://en.wikipedia.org/wiki/Bitonic_sorter

# In implementation, all sorting of individual pairs will be processed by a GPU kernel.
# Also, "nums" will be a buffer and the kernels will use variables such as lengths or depths. CPU indexing should be avoided.
# The CPU will only be responsible for setting broad compute shader instructions.

# func used by GPU kernels
# Will use tuples instead though
def SortPair(a, b):
    if a > b:
        high = a
        low = b
    else:
        high = b
        low = a
    return low, high

# GPU kernel
def BrownSort(nums, length):
    for i in range(int(length/2)):
        nums[i], nums[length-i-1] = SortPair(nums[i], nums[length-i-1])
    return nums

# GPU kernel
def PinkSort(nums, length):
    hLen = int(length / 2)
    for i in range(hLen):
        nums[i], nums[i+hLen] = SortPair(nums[i], nums[i+hLen])
    return nums

# CPU?
def BlueSort(nums):
    length = len(nums)
    nums = BrownSort(nums, length)
    depth = int(log2(length))
    for i in range(1, depth):
        PinkBoxesNum = pow(2, i)
        PinkLen = length // PinkBoxesNum
        for j in range(PinkBoxesNum):
            start = j * PinkLen
            end = start + PinkLen
            nums[start:end] = PinkSort(nums[start:end], PinkLen)
    return nums

# CPU?
def BitonicSort(nums):
    global ioor
    length = len(nums)
    fullDepth = int(ceil(log2(length)))
    
    if fullDepth != log2(len(nums)):
        extraValuesNum = pow(2, fullDepth) - len(nums)
        nums += [ioor] * extraValuesNum
        length = len(nums)
        
    for i in range(fullDepth):
        BlueBoxesNum = 2 ** (fullDepth - i)
        BlueLen = length // BlueBoxesNum
        for j in range(BlueBoxesNum):
            start = j * BlueLen
            end = start + BlueLen
            nums[start:end] = BlueSort(nums[start:end])
    return nums

# Test
ioor = 2000
print(BitonicSort([9, 3, 5, 7, 9, 3, 5, 7, 9, 3, 5, 7, 9, 3, 5, 7, 9, 3, 5, 7, 9, 3, 5, 7, 9, 3, 5, 7, 9, 3, 5, 7, 5, 7]))