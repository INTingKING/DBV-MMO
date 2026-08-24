using Unity.Netcode;
using UnityEngine;

public class EnemyAnimation : NetworkBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private EnemyAI enemyAI;

    [Header("Sprites")]
    [SerializeField] private Texture2D idleSheet;
    [SerializeField] private Texture2D walkSheet;
    [SerializeField] private Texture2D attackSheet;

    [HideInInspector] [SerializeField] private Sprite[] idleSprites;
    [HideInInspector] [SerializeField] private Sprite[] moveSprites;
    [HideInInspector] [SerializeField] private Sprite[] attackSprites;
    [SerializeField] private float idleFramesPerSecond = 6f;
    [SerializeField] private float walkFramesPerSecond = 8f;
    [SerializeField] private float attackFramesPerSecond = 12f;

    [Header("Motion / facing")]
    [SerializeField] private float walkEnterSpeed = 0.35f;
    [SerializeField] private float walkExitSpeed = 0.15f;
    [SerializeField] private bool flipWhenMovingLeft = true;
    [SerializeField] private bool faceNearestPlayer = true;
    [SerializeField] private float faceDeadZone = 0.05f;

    [SerializeField] private Texture2D slamSheet;
    [HideInInspector] [SerializeField] private Sprite[] slamSprites;
    [SerializeField] private float slamFramesPerSecond = 12f;

    private Sprite[] _idleFrames = System.Array.Empty<Sprite>();
    private Sprite[] _walkFrames = System.Array.Empty<Sprite>();
    private Sprite[] _attackFrames = System.Array.Empty<Sprite>();
    private Sprite[] _slamFrames = System.Array.Empty<Sprite>();
    private bool _slamming;
    private Vector3 _lastPosition;
    private Vector2 _smoothedVelocity;
    private bool _moving;
    private bool _attacking;
    private bool _facingLeft;
    private float _frameTimer;
    private int _frameIndex;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();

        CacheFrames();
        _lastPosition = transform.position;
    }

    public override void OnNetworkSpawn()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();

        CacheFrames();
        _lastPosition = transform.position;
        _smoothedVelocity = Vector2.zero;
        _moving = false;
        _attacking = false;
        _slamming = false;
        _facingLeft = false;
        ResetFramePlayback();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            spriteRenderer.flipX = false;
        }
    }

    private void OnValidate()
    {
        Sprite[] idle = SpriteSheetFrames.LoadSorted(idleSheet);
        if (idle != null)
            idleSprites = idle;

        Sprite[] walk = SpriteSheetFrames.LoadSorted(walkSheet);
        if (walk != null)
            moveSprites = walk;

        Sprite[] attack = SpriteSheetFrames.LoadSorted(attackSheet);
        if (attack != null)
            attackSprites = attack;

        Sprite[] slam = SpriteSheetFrames.LoadSorted(slamSheet);
        if (slam != null)
            slamSprites = slam;

        CacheFrames();
    }

    [ContextMenu("Reload Animation Sheets")]
    private void ReloadAnimationSheets()
    {
        OnValidate();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public void ServerPlayAutoAttack()
    {
        if (!IsServer || !IsSpawned)
            return;

        PlayAutoAttackClientRpc();
    }

    public void ServerPlaySlam()
    {
        if (!IsServer || !IsSpawned)
            return;

        PlaySlamClientRpc();
    }

    [ClientRpc]
    private void PlayAutoAttackClientRpc()
    {
        PlayAutoAttackLocal();
    }

    [ClientRpc]
    private void PlaySlamClientRpc()
    {
        PlaySlamLocal();
    }

    private void PlayAutoAttackLocal()
    {
        if (_attackFrames == null || _attackFrames.Length == 0)
            return;

        _slamming = false;
        _attacking = true;
        _frameIndex = 0;
        _frameTimer = 0f;
        ShowCurrentFrame();
        GameSfx.PlayEnemyAttack();
    }

    private void PlaySlamLocal()
    {
        Sprite[] frames = _slamFrames != null && _slamFrames.Length > 0 ? _slamFrames : _attackFrames;
        if (frames == null || frames.Length == 0)
            return;

        _attacking = false;
        _slamming = true;
        _frameIndex = 0;
        _frameTimer = 0f;
        ShowCurrentFrame();
        GameSfx.PlayBossSlam();
    }

    private void LateUpdate()
    {
        if (!IsSpawned)
            return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - _lastPosition;
        _lastPosition = pos;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector2 instantVelocity = new Vector2(delta.x, delta.y) / dt;
        _smoothedVelocity = Vector2.Lerp(_smoothedVelocity, instantVelocity, 1f - Mathf.Exp(-12f * dt));

        if (!_attacking && !_slamming)
        {
            bool wantMove = DetectMoving();
            if (wantMove != _moving)
            {
                _moving = wantMove;
                ResetFramePlayback();
            }
        }

        UpdateFacing(instantVelocity.x);
        TickSpriteFrames();
    }

    private bool DetectMoving()
    {
        float speed = _smoothedVelocity.magnitude;
        if (_moving)
            return speed > walkExitSpeed;
        return speed > walkEnterSpeed;
    }

    private void UpdateFacing(float velocityX)
    {
        if (spriteRenderer == null)
            return;

        if (faceNearestPlayer && TryFaceNearestPlayer())
        {
            spriteRenderer.flipX = _facingLeft;
            return;
        }

        if (!flipWhenMovingLeft)
            return;

        float faceThreshold = walkEnterSpeed * 0.5f;
        if (Mathf.Abs(velocityX) > faceThreshold)
            _facingLeft = velocityX < 0f;

        spriteRenderer.flipX = _facingLeft;
    }

    private bool TryFaceNearestPlayer()
    {
        if (enemyAI == null)
            enemyAI = GetComponent<EnemyAI>();
        if (enemyAI == null)
            return false;

        Transform target = enemyAI.CurrentTarget ?? enemyAI.FindNearestLivingPlayer();
        if (target == null)
            return false;

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist > enemyAI.AggroRange)
            return false;

        float dx = target.position.x - transform.position.x;
        if (Mathf.Abs(dx) >= faceDeadZone)
            _facingLeft = dx < 0f;

        return true;
    }

    private void CacheFrames()
    {
        _idleFrames = CompactSprites(idleSprites);
        _walkFrames = CompactSprites(moveSprites);
        _attackFrames = CompactSprites(attackSprites);
        _slamFrames = CompactSprites(slamSprites);

        if (_idleFrames.Length == 0)
            _idleFrames = _walkFrames;
        if (_walkFrames.Length == 0)
            _walkFrames = _idleFrames;
        if (_slamFrames.Length == 0)
            _slamFrames = _attackFrames;
    }

    private static Sprite[] CompactSprites(Sprite[] source)
    {
        if (source == null || source.Length == 0)
            return System.Array.Empty<Sprite>();

        int count = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
                count++;
        }

        if (count == 0)
            return System.Array.Empty<Sprite>();
        if (count == source.Length)
            return source;

        Sprite[] compact = new Sprite[count];
        int w = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
                compact[w++] = source[i];
        }

        return compact;
    }

    private void ResetFramePlayback()
    {
        if (_attacking || _slamming)
            return;

        _frameIndex = 0;
        _frameTimer = 0f;
        ShowCurrentFrame();
    }

    private void TickSpriteFrames()
    {
        Sprite[] list = GetCurrentFrames();
        if (list == null || list.Length == 0)
            return;

        if (list.Length == 1)
        {
            _frameIndex = 0;
            ShowCurrentFrame();
            if (_attacking || _slamming)
                EndAttack();
            return;
        }

        float fps = Mathf.Max(0.1f, ActionFramesPerSecond());
        _frameTimer += Time.deltaTime;
        float step = 1f / fps;

        if (_frameTimer >= step)
        {
            _frameTimer -= step;
            if (_frameTimer >= step)
                _frameTimer = 0f;

            _frameIndex++;
            if (_frameIndex >= list.Length)
            {
                if (_attacking || _slamming)
                {
                    EndAttack();
                    return;
                }

                _frameIndex = 0;
            }

            ShowCurrentFrame();
        }
    }

    private void EndAttack()
    {
        _attacking = false;
        _slamming = false;
        _frameIndex = 0;
        _frameTimer = 0f;
        ShowCurrentFrame();
    }

    private float ActionFramesPerSecond()
    {
        if (_slamming)
            return slamFramesPerSecond;
        if (_attacking)
            return attackFramesPerSecond;
        return _moving ? walkFramesPerSecond : idleFramesPerSecond;
    }

    private Sprite[] GetCurrentFrames()
    {
        if (_slamming && _slamFrames.Length > 0)
            return _slamFrames;
        if (_attacking && _attackFrames.Length > 0)
            return _attackFrames;
        return _moving ? _walkFrames : _idleFrames;
    }

    private void ShowCurrentFrame()
    {
        if (spriteRenderer == null)
            return;

        Sprite[] list = GetCurrentFrames();
        if (list == null || list.Length == 0)
            return;

        if (_frameIndex < 0 || _frameIndex >= list.Length)
            _frameIndex = 0;

        Sprite frame = list[_frameIndex];
        if (frame != null && spriteRenderer.sprite != frame)
            spriteRenderer.sprite = frame;
    }
}
