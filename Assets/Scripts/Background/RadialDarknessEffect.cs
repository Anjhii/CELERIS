using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Custom/Radial Darkness")]
public class RadialDarknessEffect : VolumeComponent, IPostProcessComponent
{
    public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));
    public ClampedFloatParameter radius = new ClampedFloatParameter(0.3f, 0f, 1f);
    public ClampedFloatParameter softness = new ClampedFloatParameter(0.2f, 0f, 1f);
    public ColorParameter color = new ColorParameter(Color.black);

    public bool IsActive() => radius.value > 0f;
    public bool IsTileCompatible() => false;
}