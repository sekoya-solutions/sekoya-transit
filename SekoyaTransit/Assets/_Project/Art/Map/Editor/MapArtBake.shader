// Editor-only shader for the map art bake tool (ADR 0032): the Kenney color map lit by one fixed directional light
// plus a flat ambient term, opaque, no shadows. It is only used by command buffers in MapArtBaker, so it does not
// depend on the render pipeline or its 2D renderer, and it is never referenced by the player build.
Shader "Hidden/SekoyaTransit/MapArtBake"
{
    Properties
    {
        _MainTex ("Color map", 2D) = "white" {}
        _LightDirection ("Direction toward the light (world)", Vector) = (0, 1, 0, 0)
        _Ambient ("Ambient", Float) = 0.55
        _Diffuse ("Diffuse", Float) = 0.55
    }

    SubShader
    {
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _LightDirection;
            float _Ambient;
            float _Diffuse;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.normal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.normal);
                float light = _Ambient + (_Diffuse * saturate(dot(normal, normalize(_LightDirection.xyz))));
                float3 color = tex2D(_MainTex, input.uv).rgb * light;

                // The bake reads the target back as raw bytes, so write sRGB-encoded color explicitly.
                #if !defined(UNITY_COLORSPACE_GAMMA)
                color = LinearToGammaSpace(color);
                #endif
                return float4(color, 1);
            }
            ENDCG
        }
    }
}
