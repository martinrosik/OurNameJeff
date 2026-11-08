// Editor-only shader used by ModelSpriteRenderer to bake 3D models into 2D sprites:
// texture colour with simple fixed-direction lighting, so low-poly faces read clearly without scene lights.
Shader "Hidden/ResortEmpire/ModelSpriteBake"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _LightDir ("Light Direction (world)", Vector) = (-0.35, 1, -0.55, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.58
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _LightDir;
            float _Ambient;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 colour = tex2D(_MainTex, i.uv) * _Color;
                float diffuse = saturate(dot(normalize(i.normal), normalize(_LightDir.xyz)));
                colour.rgb *= _Ambient + (1.0 - _Ambient) * diffuse;
                colour.a = 1;
                return colour;
            }
            ENDCG
        }
    }
}
