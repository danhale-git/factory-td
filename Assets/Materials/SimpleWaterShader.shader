Shader "Terrain/WaterShader"
{
  Properties
  {
      _MaskTex ("Bump Mask", 2D) = "white" {}
      _BumpMap ("Bumpmap", 2D) = "bump" {}
  }
 
  SubShader
  {
      Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
      LOD 300
   
    CGPROGRAM
    #pragma surface surf BlinnPhong vertex:vert alpha:fade

    sampler2D _BumpMap;
    sampler2D _MaskTex;

    struct Input
    {
        float2 uv_MaskTex;
        float2 uv_BumpMap;
        float4 vertColors;
    };
 
    void vert(inout appdata_full v, out Input o)
    {
        UNITY_INITIALIZE_OUTPUT(Input,o);
        o.vertColors= v.color.rgba;
        o.uv_BumpMap = v.texcoord;
    }
 
 
    void surf (Input IN, inout SurfaceOutput o)
    {
        o.Albedo = IN.vertColors.rgb;
        o.Alpha = IN.vertColors.a;

        //float nMask = tex2D(_MaskTex, IN.uv_MaskTex).a;

        //o.Normal = nMask > 0.9 ? UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap)) : o.Normal;    //  uncomment for NORMS
    }
    ENDCG
  }
 
  Fallback "Specular"
}