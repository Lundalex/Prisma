from math import ceil, log2, pow

# compexity not exact
# time complexity: O(log2(n))
# threadsNum complexity: O(n - log2(n-2))
# -> time complexity for large sets: O((n - log2(n-2)) * log2(n)) = ~O(n) for large sets
def ParallelPrefixSum(input_array):
    input_array_len = len(input_array)
    input_array_len_log2_ciel = ceil(log2(input_array_len))
    input_array_ceil_len = pow(2, input_array_len_log2_ciel)
    offset = 1/2
    
    dBufferA = input_array.copy()
    # dBufferB does not have to be set in compute shader implementation
    dBufferB = input_array.copy()
    
    # c# side
    bufferCycle = False
    for iteration in range(1, input_array_len_log2_ciel+1):
        # bufferCycle == True -> dBufferA is read buffer, dBufferB is write buffer
        # bufferCycle == False -> dBufferB is read buffer, dBufferA is write buffer
        bufferCycle = not bufferCycle
        offset = int(offset*2)
        halfOffset = 0 if offset == 1 else int(offset / 2)
        totIndicesToProcess = input_array_len - offset
        
        # compute shader side
        # variables: bufferCycle, offset
        if bufferCycle:
            for id_x in range(totIndicesToProcess):
                i = id_x + offset
                if i < input_array_ceil_len:
                    storedVal = dBufferA[i] + dBufferA[i - offset]
                    dBufferB[i] = storedVal
                
            for id_x in range(halfOffset):
                i = halfOffset + id_x
                dBufferB[i] = dBufferA[i]
                
        else:
            for id_x in range(totIndicesToProcess):
                i = id_x + offset
                if i < input_array_ceil_len:
                    storedVal = dBufferB[i] + dBufferB[i - offset]
                    dBufferA[i] = storedVal
                
            for id_x in range(halfOffset):
                i = halfOffset + id_x
                dBufferA[i] = dBufferB[i]
            
    if bufferCycle:
        return dBufferB
    else:
        return dBufferA
    
input_array = [1, 2, 3, 4, 412, 6, 7, 12, 13, 1, 1, 1, 4, 5, 28, 21]
output_array = ParallelPrefixSum(input_array)
print(output_array)