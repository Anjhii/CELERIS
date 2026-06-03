using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadialDarknessRendererFeature : ScriptableRendererFeature
{
    class RadialDarknessPass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle tempTexture;

        public RadialDarknessPass(Material mat)
        {
            material = mat;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            RenderingUtils.ReAllocateIfNeeded(ref tempTexture, desc, name: "_TempRadialDarkness");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            var stack = VolumeManager.instance.stack;
            var effect = stack.GetComponent<RadialDarknessEffect>();
            if (effect == null || !effect.IsActive()) return;

            material.SetVector("_Center", new Vector4(effect.center.value.x, effect.center.value.y, 0, 0));
            material.SetFloat("_Radius", effect.radius.value);
            material.SetFloat("_Softness", effect.softness.value);
            material.SetColor("_Color", effect.color.value);

            CommandBuffer cmd = CommandBufferPool.Get("RadialDarkness");
            RTHandle cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            if (tempTexture == null || tempTexture.rt == null) return;
            Blitter.BlitCameraTexture(cmd, cameraTarget, tempTexture);
            Blitter.BlitCameraTexture(cmd, tempTexture, cameraTarget, material, 0);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // cleanup si es necesario
        }

        public void Dispose()
        {
            tempTexture?.Release();
        }
    }

    private RadialDarknessPass pass;
    private Material material;

    public override void Create()
    {
        Shader shader = Shader.Find("Custom/RadialDarkness");
        if (shader == null)
        {
            Debug.LogError("RadialDarkness shader not found!");
            return;
        }
        material = new Material(shader);
        pass = new RadialDarknessPass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass != null)
            renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        if (material != null)
            CoreUtils.Destroy(material);
    }
}