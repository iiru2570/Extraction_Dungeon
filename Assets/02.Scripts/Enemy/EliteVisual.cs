using UnityEngine;

// 정예 몬스터를 일반 몬스터와 구분되게 보이도록 크기와 색을 바꿉니다 (임시 구분용, 연출은 Day 13).
// 정예 프리팹 루트에 붙입니다. 셰이더가 URP(_BaseColor)든 Built-in(_Color)이든 동작합니다.
public class EliteVisual : MonoBehaviour
{
    [Tooltip("모델 크기 배율. 루트 Scale 에 곱해집니다")]
    [SerializeField, Min(0.5f)] private float scaleMultiplier = 1.2f;
    [SerializeField] private Color tint = new Color(1f, 0.55f, 0.2f, 1f);
    [Tooltip("원래 색과 tint 를 섞는 비율 (0 = 원래 색, 1 = tint 로 완전 변경)")]
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.5f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        transform.localScale *= scaleMultiplier;

        var block = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mat = r.sharedMaterial;
            if (mat == null) continue;

            int id = mat.HasProperty(BaseColorId) ? BaseColorId : (mat.HasProperty(ColorId) ? ColorId : -1);
            if (id < 0) continue;

            r.GetPropertyBlock(block);
            block.SetColor(id, Color.Lerp(mat.GetColor(id), tint, tintStrength));
            r.SetPropertyBlock(block);
        }
    }
}
