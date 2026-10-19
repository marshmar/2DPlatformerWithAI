using UnityEngine;

namespace Platformer
{
    /// <summary>
    /// 조준점과 갈고리 대상 마커. 둘 다 Billboard.shader 로 그려서 카메라가 흔들리거나 기울어도(쉐이크 롤)
    /// 항상 똑바로, 화면에서 같은 픽셀 크기로 보인다.
    /// </summary>
    public sealed class AimReticle : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");

        [SerializeField] SpriteRenderer reticle;
        [SerializeField] SpriteRenderer marker;
        [ColorUsage(false, true)]
        [SerializeField] Color aimColor = new(0.4f, 2f, 2.4f, 1f);
        [ColorUsage(false, true)]
        [SerializeField] Color terrainTargetColor = new(0.4f, 2.6f, 2.2f, 1f);
        [ColorUsage(false, true)]
        [SerializeField] Color enemyTargetColor = new(3.2f, 0.5f, 0.8f, 1f);

        PlayerController player;
        GrappleHook hook;
        MaterialPropertyBlock block;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            hook = GetComponent<GrappleHook>();
            block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            Vector2 aimPoint = player.Input.AimPoint(player.BodyCenter, hook.Config.Range * 0.6f);
            reticle.transform.position = aimPoint;
            SetLook(reticle, aimColor, 0f);

            GrappleHook.Target target = hook.Preview;
            marker.enabled = target.Valid;
            if (target.Valid)
            {
                marker.transform.position = target.Point;
                SetLook(marker, target.Enemy != null ? enemyTargetColor : terrainTargetColor, 1f);
            }
        }

        void SetLook(SpriteRenderer spriteRenderer, Color color, float shape)
        {
            spriteRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            block.SetFloat(ShapeId, shape);
            spriteRenderer.SetPropertyBlock(block);
        }
    }
}
