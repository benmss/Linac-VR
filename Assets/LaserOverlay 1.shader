// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "Custom/LaserOverlay 1"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Laser ("Laser", Color) = (1,1,1,1)        
        _xMin ("xMin", Float) = -0.5
        _xMax ("xMax", Float) = -0.48
    }
    SubShader
    {
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            // compile shader into multiple variants, with and without shadows
            // (we don't care about any lightmaps yet, so skip these variants)
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            // shadow helper functions and macros
            #include "AutoLight.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                SHADOW_COORDS(1) // put shadows data into TEXCOORD1
                fixed3 diff : COLOR0;
                fixed3 ambient : COLOR1;
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD1;
            };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                float3 worldPos = mul (unity_ObjectToWorld, v.vertex).xyz;
                o.wpos = worldPos;
                half3 worldNormal = UnityObjectToWorldNormal(v.normal);
                half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
                o.diff = nl * _LightColor0.rgb;
                o.ambient = ShadeSH9(half4(worldNormal,1));
                // compute shadows data
                TRANSFER_SHADOW(o)
                return o;
            }

            sampler2D _MainTex;
            fixed4 _Laser;
            fixed4 _Color;
            float _xMin;
            float _xMax;

            fixed4 frag (v2f i) : SV_Target
            {
                //fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 col = _Color;

                

                // compute shadow attenuation (1.0 = fully lit, 0.0 = fully shadowed)
                fixed shadow = SHADOW_ATTENUATION(i);
                // darken light's illumination with shadow, keep ambient intact
                fixed3 lighting = i.diff * shadow + i.ambient;
                //fixed3 lighting = i.diff + i.ambient;
                col.rgb *= lighting;
                
                if (i.wpos.x < _xMax && i.wpos.x > _xMin) {
                  float m = (_xMin + _xMax) * 0.5;
                  float s = 1 / (_xMin - m);
                  float w = float4(0.75,0.75,0.75,1) * (0 - abs (i.wpos.x - m) * s);

                  col.rgb *= _Laser + w;
                }

                return col;
            }
            ENDCG
        }

        // shadow casting support
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
}