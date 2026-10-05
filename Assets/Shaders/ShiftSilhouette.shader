// Draws a texture's SHAPE in one flat colour: the texture's alpha, the material's colour.
// Built for Shift-infused enemies (2026-10-04): ShiftInfused traces the enemy's body with it (the
// outline, the halo, the phase-slip copies, the echoes it leaves behind, the charge band sweeping
// up through it) and InfusedDeathVFX uses it for the body's last silhouette.
//
// Unlit on purpose: Shift is energy, so it glows at full value under the scene's 0.5 global light.
//
// ⚠️ LOADED THROUGH Resources/ShiftSilhouette.mat, NEVER Shader.Find. A build only contains the
// shaders something references, so a Shader.Find-only shader works in the Editor and draws
// magenta in a build (the purple-water lesson in CLAUDE.md).
//
// Everything that varies per renderer arrives through a MaterialPropertyBlock, so one material
// serves every layer of every infused enemy.
Shader "Deckshift/Shift Silhouette"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _BandColor ("Band Color", Color) = (1,1,1,1)
        _BandAlpha ("Band Alpha (added inside a band)", Float) = 0
        _BandMix ("Band Mix (how far a band pulls the colour to Band Color)", Range(0,1)) = 0
        _BandSpacing ("Band Spacing (world units)", Float) = 1.5
        _BandWidth ("Band Width (world units)", Float) = 0.0625
        _BandOffset ("Band Offset (world units, driven by script)", Float) = 0
        _Origin ("Origin (world point the pixel grid is anchored to)", Vector) = (0,0,0,0)
        _PixelSize ("World units per pixel", Float) = 0.03125
        _Dissolve ("Dissolve (0 whole, 1 gone)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _BandColor;
            float _BandAlpha, _BandMix, _BandSpacing, _BandWidth, _BandOffset, _PixelSize, _Dissolve;
            float4 _Origin;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy - _Origin.xy;
                o.color = v.color;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed a = tex2D(_MainTex, i.uv).a * i.color.a;

                // Snapped to the body's own pixel grid, so a band is a whole row of pixels and the
                // dissolve eats whole pixels: smooth gradients would read as a different art style.
                float2 px = floor(i.world / _PixelSize);
                float y = px.y * _PixelSize - _BandOffset;
                float m = y - _BandSpacing * floor(y / _BandSpacing);   // distance into this period
                float band = 1.0 - step(_BandWidth, m);

                fixed3 rgb = lerp(_Color.rgb, _BandColor.rgb, band * _BandMix) * i.color.rgb;
                fixed alpha = a * saturate(_Color.a + band * _BandAlpha);
                alpha *= step(_Dissolve, hash(px) * 0.999);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
