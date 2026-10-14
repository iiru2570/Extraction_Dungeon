using System.Collections;
using UnityEngine;

// 타격 테스트용 훈련 더미. 캡슐/큐브 등에 Health 와 함께 붙입니다.
//  - 맞으면 잠깐 붉게 번쩍임
//  - 죽으면 잠시 뒤 체력을 되돌려 계속 테스트 가능
// 나중에 "공격하는 더미"(공격 예고 -> 공격)로 확장할 베이스입니다.
[RequireComponent(typeof(Health))]
public class TrainingDummy : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;   // 비우면 자식에서 자동 검색
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashTime = 0.1f;
    [SerializeField] private float respawnDelay = 2f;

    private Health health;
    private MaterialPropertyBlock block;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor"); // URP Lit
    private static readonly int ColorIdBuiltIn = Shader.PropertyToID("_Color"); // Built-in
    private Coroutine flashRoutine;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void OnDamaged(DamageInfo info)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    private void OnDied()
    {
        StartCoroutine(Respawn());
    }

    private IEnumerator Flash()
    {
        SetColor(hitColor);
        yield return new WaitForSeconds(flashTime);
        ClearColor();
        flashRoutine = null;
    }

    private IEnumerator Respawn()
    {
        yield return new WaitForSeconds(respawnDelay);
        health.ResetHealth();
        Debug.Log($"[{name}] 체력 복구");
    }

    private void SetColor(Color c)
    {
        if (targetRenderer == null) return;
        targetRenderer.GetPropertyBlock(block);
        block.SetColor(ColorId, c);
        block.SetColor(ColorIdBuiltIn, c);
        targetRenderer.SetPropertyBlock(block);
    }

    private void ClearColor()
    {
        if (targetRenderer == null) return;
        targetRenderer.SetPropertyBlock(null);
    }
}
