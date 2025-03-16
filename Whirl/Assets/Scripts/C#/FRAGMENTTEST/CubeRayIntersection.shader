Shader "Unlit/RigidBodyRayIntersection"
{
    Properties
    {
        _RigidBodyData ("Rigid Body Data Texture", 2D) = "white" {}
        _VertexData ("Vertex Data Texture", 2D) = "white" {}
        _NumRigidBodies ("Number of RigidBodies", Int) = 10
        _NumVertexElements ("Number of Vertex Elements", Int) = 40
        _SphereRadius ("Sphere Radius", Float) = 0.03
        _Zoom ("Zoom", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Uniforms for rigid body data
            sampler2D _RigidBodyData;
            uint _NumRigidBodies;
            uint _RigidBodyTexWidth;
            uint _RigidBodyTexHeight;
            float _RigidBodyInvTexWidth;
            float _RigidBodyInvTexHeight;
            int _RigidFloatsPerElement;

            // Uniforms for vertex data
            sampler2D _VertexData;
            uint _NumVertexElements;
            uint _VertexTexWidth;
            uint _VertexTexHeight;
            float _VertexInvTexWidth;
            float _VertexInvTexHeight;
            int _VertexFloatsPerElement;

            float _SphereRadius;
            float _Zoom;

            // Rigid body: _RigidFloatsPerElement floats per element
            float GetRigidBodyFloat(uint elementIndex, uint fieldOffset)
            {
                uint flatIndex = elementIndex * (uint)_RigidFloatsPerElement + fieldOffset;
                uint x = flatIndex % _RigidBodyTexWidth;
                uint y = flatIndex / _RigidBodyTexWidth;
                float2 uv = float2((x + 0.5) * _RigidBodyInvTexWidth, (y + 0.5) * _RigidBodyInvTexHeight);
                return tex2D(_RigidBodyData, uv).r;
            }
            float2 GetRigidBodyFloat2(uint elementIndex, uint fieldOffset)
            {
                return float2(GetRigidBodyFloat(elementIndex, fieldOffset), GetRigidBodyFloat(elementIndex, fieldOffset + 1));
            }

            // Vertex data: _VertexFloatsPerElement floats per element (parentIndex, pos.x, pos.y)
            float GetVertexFloat(uint elementIndex, uint fieldOffset)
            {
                uint flatIndex = elementIndex * (uint)_VertexFloatsPerElement + fieldOffset;
                uint x = flatIndex % _VertexTexWidth;
                uint y = flatIndex / _VertexTexWidth;
                float2 uv = float2((x + 0.5) * _VertexInvTexWidth, (y + 0.5) * _VertexInvTexHeight);
                return tex2D(_VertexData, uv).r;
            }
            int GetVertexInt(uint elementIndex)
            {
                return (int)GetVertexFloat(elementIndex, 0);
            }
            float2 GetVertexFloat2(uint elementIndex, uint fieldOffset)
            {
                return float2(GetVertexFloat(elementIndex, fieldOffset), GetVertexFloat(elementIndex, fieldOffset + 1));
            }

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 fragPos = (i.uv - 0.5) / _Zoom + 0.5;

                // Loop over vertex data
                [unroll(200)]
                for (uint j = 0; j < _NumVertexElements; j++)
                {
                    int parentIndex = GetVertexInt(j);
                    
                    // Compute worldPos: rigid body pos is at field offset 0
                    float2 parentPos = GetRigidBodyFloat2(parentIndex, 0);
                    // Vertex offset stored in vertex data at field offset 1 (pos.x, pos.y)
                    float2 vertexOffset = GetVertexFloat2(j, 1);
                    float2 worldPos = parentPos + vertexOffset;
                    
                    // Compute distance from fragment to this vertex.
                    float dst = length(fragPos - worldPos);
                    if (dst < _SphereRadius) return float4(1, 0, 0, 1);
                }
                return float4(0, 0, 0, 1);
            }
            ENDCG
        }
    }
}