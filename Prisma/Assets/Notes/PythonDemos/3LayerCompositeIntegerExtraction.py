def decompose_last_chunk_key_ptype_porder(last_chunk_key_ptype_porder, chunk_num, ptypes_num):
    p_order = last_chunk_key_ptype_porder // (chunk_num * ptypes_num)
    p_type = (last_chunk_key_ptype_porder % (chunk_num * ptypes_num)) // chunk_num
    last_chunk_key = last_chunk_key_ptype_porder % chunk_num
    return last_chunk_key, p_type, p_order

def decompose_last_chunk_key(last_chunk_key_ptype_porder, chunk_num):
    return last_chunk_key_ptype_porder % chunk_num

def decompose_ptype(last_chunk_key_ptype_porder, chunk_num, ptypes_num):
    return (last_chunk_key_ptype_porder % (chunk_num * ptypes_num)) // chunk_num

def decompose_porder(last_chunk_key_ptype_porder, chunk_num, ptypes_num):
    return last_chunk_key_ptype_porder // (chunk_num * ptypes_num)

def compose_last_chunk_key_ptype_porder(p_order, p_type, last_chunk_key, chunk_num, ptypes_num):
    p_order_value = p_order * chunk_num * ptypes_num
    p_type_value = p_type * chunk_num
    last_chunk_key_value = last_chunk_key
    composed_last_chunk_key_ptype_porder = p_order_value + p_type_value + last_chunk_key_value
    return composed_last_chunk_key_ptype_porder

# Example usage:
chunk_num = 100  # Example value
ptypes_num = 5   # Example value
last_chunk_key_ptype_porder = 12345  # Example value for decomposing

# Decompose
last_chunk_key, p_type, p_order = decompose_last_chunk_key_ptype_porder(last_chunk_key_ptype_porder, chunk_num, ptypes_num)
print(f"Decomposed: LastChunkKey = {last_chunk_key}, PType = {p_type}, POrder = {p_order}")

# Compose
composed_value = compose_last_chunk_key_ptype_porder(p_order, p_type, last_chunk_key, chunk_num, ptypes_num)
print(f"Composed value: {composed_value}")
