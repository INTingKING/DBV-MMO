using Unity.Netcode;
using UnityEngine;

public class PlayerClassAnimation : NetworkBehaviour
{
    private enum ActionKind
    {
        None,
        AutoAttack,
        Skill
    }

    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Speed (units/sec) above this counts as walking for remote players.")]
    [SerializeField] private float walkEnterSpeed = 0.35f;

    [Tooltip("Speed (units/sec) below this returns to idle for remote players. Keep lower than Walk Enter for stability.")]
    [SerializeField] private float walkExitSpeed = 0.15f;

    [Tooltip("Sprites face right. With no target, moving left flips flipX.")]
    [SerializeField] private bool flipWhenMovingLeft = true;

    [Tooltip("If true, face the currently targeted enemy (overrides move facing while targeted).")]
    [SerializeField] private bool faceTargetedEnemy = true;

    [Header("Warrior (Knight) — idle, walk, auto-attack, ability [1]")]
    [SerializeField] private ClassAnimationSet warrior = new ClassAnimationSet();

    [Header("Mage (Wizard) — idle, walk, auto-attack, ability [1]")]
    [SerializeField] private ClassAnimationSet mage = new ClassAnimationSet();

    private PlayerClass _playerClass;
    private Player _player;
    private PlayerCombat _combat;
    private ClassAnimationSet _active;
    private Sprite[] _idleFrames = System.Array.Empty<Sprite>();
    private Sprite[] _walkFrames = System.Array.Empty<Sprite>();
    private Sprite[] _attackFrames = System.Array.Empty<Sprite>();
    private Sprite[] _skillFrames = System.Array.Empty<Sprite>();
    private Vector3 _lastPosition;
    private Vector2 _smoothedVelocity;
    private bool _moving;
    private bool _facingLeft;
    private float _frameTimer;
    private int _frameIndex;
    private ActionKind _action = ActionKind.None;
    private Sprite[] _actionFrames = System.Array.Empty<Sprite>();
    private float _actionFps = 12f;

    public ClassAnimationSet WarriorAnimations => warrior;
    public ClassAnimationSet MageAnimations => mage;

    private void Awake()
    {
        EnsureSpriteRenderer();
        _playerClass = GetComponent<PlayerClass>();
        _player = GetComponent<Player>();
        _combat = GetComponent<PlayerCombat>();
        _lastPosition = transform.position;
    }

    private void EnsureSpriteRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            return;

        Transform visual = transform.Find("Visual");
        if (visual == null)
        {
            GameObject go = new GameObject("Visual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            visual = go.transform;
        }

        spriteRenderer = visual.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = visual.gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 10;
    }

    public override void OnNetworkSpawn()
    {
        EnsureSpriteRenderer();

        if (_playerClass == null)
            _playerClass = GetComponent<PlayerClass>();
        if (_player == null)
            _player = GetComponent<Player>();
        if (_combat == null)
            _combat = GetComponent<PlayerCombat>();

        if (_playerClass != null)
            _playerClass.ClassChanged += HandleClassChanged;

        ApplyForClass(_playerClass != null ? _playerClass.CurrentClass : PlayerClassType.None);
        _lastPosition = transform.position;
        _smoothedVelocity = Vector2.zero;
    }

    public override void OnNetworkDespawn()
    {
        if (_playerClass != null)
            _playerClass.ClassChanged -= HandleClassChanged;
    }

    public override void OnDestroy()
    {
        if (_playerClass != null)
            _playerClass.ClassChanged -= HandleClassChanged;

        base.OnDestroy();
    }

    public void PlayAutoAttack()
    {

        if (_action == ActionKind.Skill)
            return;

        if (IsMage() && IsLocomoting())
            return;

        StartAction(ActionKind.AutoAttack, _attackFrames, _active != null ? _active.attackFramesPerSecond : 12f);
    }

    public void PlaySkill()
    {

        StartAction(ActionKind.Skill, _skillFrames, _active != null ? _active.skillFramesPerSecond : 12f);
    }

    private bool IsMage()
    {
        return _playerClass != null && _playerClass.CurrentClass == PlayerClassType.Mage;
    }

    private bool IsLocomoting()
    {
        if (IsOwner && _player != null)
            return _player.IsTryingToMove;
        return _moving;
    }

    private void StartAction(ActionKind kind, Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0)
            return;

        _action = kind;
        _actionFrames = frames;
        _actionFps = Mathf.Max(0.1f, fps);
        _frameIndex = 0;
        _frameTimer = 0f;
        ShowCurrentFrame();
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

        bool wantMove = DetectWantMove(instantVelocity);
        if (_action == ActionKind.None)
        {
            if (wantMove != _moving)
            {
                _moving = wantMove;
                ResetFramePlayback();
            }
        }
        else if (_action == ActionKind.AutoAttack && IsMage() && wantMove)
        {

            _moving = true;
            EndAction();
        }
        else
        {
            _moving = wantMove;
        }

        UpdateFacing(instantVelocity.x);
        TickSpriteFrames();
    }

    private bool DetectWantMove(Vector2 instantVelocity)
    {
        if (IsOwner && _player != null)
            return _player.IsTryingToMove;

        float speed = _smoothedVelocity.magnitude;
        if (_moving)
            return speed > walkExitSpeed;
        return speed > walkEnterSpeed;
    }

    private void UpdateFacing(float velocityX)
    {
        if (spriteRenderer == null)
            return;

        if (faceTargetedEnemy && TryGetTargetFacing(out bool faceLeftFromTarget))
        {
            _facingLeft = faceLeftFromTarget;
            spriteRenderer.flipX = _facingLeft;
            return;
        }

        if (!flipWhenMovingLeft)
            return;

        float faceThreshold = IsOwner ? 0.01f : walkEnterSpeed * 0.5f;

        if (IsOwner && _player != null && Mathf.Abs(_player.MoveInput.x) > faceThreshold)
            _facingLeft = _player.MoveInput.x < 0f;
        else if (Mathf.Abs(velocityX) > faceThreshold)
            _facingLeft = velocityX < 0f;

        spriteRenderer.flipX = _facingLeft;
    }

    private bool TryGetTargetFacing(out bool faceLeft)
    {
        faceLeft = _facingLeft;

        if (_combat == null)
            _combat = GetComponent<PlayerCombat>();

        if (_combat == null || !_combat.HasTarget)
            return false;

        if (!_combat.TryGetCurrentTarget(out NetworkObject targetObject, out NetworkHealth targetHealth))
            return false;

        if (targetObject == null || targetHealth == null || targetHealth.IsDead)
            return false;

        float dx = targetObject.transform.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f)
            return true;

        faceLeft = dx < 0f;
        return true;
    }

    private void HandleClassChanged(PlayerClassType type)
    {
        ApplyForClass(type);
    }

    public void ApplyForClass(PlayerClassType type)
    {
        EnsureSpriteRenderer();

        _active = ResolveSet(type);
        CacheFrameLists(_active);
        _facingLeft = false;
        _smoothedVelocity = Vector2.zero;
        _moving = false;
        _action = ActionKind.None;
        _actionFrames = System.Array.Empty<Sprite>();
        ResetFramePlayback();

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
            spriteRenderer.flipX = false;
            spriteRenderer.enabled = true;
        }

        ShowCurrentFrame();
    }

    private ClassAnimationSet ResolveSet(PlayerClassType type)
    {
        switch (type)
        {
            case PlayerClassType.Warrior:
                return warrior;
            case PlayerClassType.Mage:
                return mage;
            default:
                return warrior != null && warrior.HasAnyVisual ? warrior : mage;
        }
    }

    private void CacheFrameLists(ClassAnimationSet set)
    {
        _idleFrames = CompactSprites(set != null ? set.idleSprites : null);
        _walkFrames = CompactSprites(set != null ? set.moveSprites : null);
        _attackFrames = CompactSprites(set != null ? set.attackSprites : null);
        _skillFrames = CompactSprites(set != null ? set.skillSprites : null);

        if (_idleFrames.Length == 0)
            _idleFrames = _walkFrames;
        if (_walkFrames.Length == 0)
            _walkFrames = _idleFrames;
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
        if (_action != ActionKind.None)
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
            if (_action != ActionKind.None)
                EndAction();
            return;
        }

        float fps = GetCurrentFps();
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
                if (_action != ActionKind.None)
                {
                    EndAction();
                    return;
                }

                _frameIndex = 0;
            }

            ShowCurrentFrame();
        }
    }

    private void EndAction()
    {
        _action = ActionKind.None;
        _actionFrames = System.Array.Empty<Sprite>();
        _frameIndex = 0;
        _frameTimer = 0f;
        ShowCurrentFrame();
    }

    private float GetCurrentFps()
    {
        if (_action == ActionKind.Skill)
            return _actionFps;

        if (IsMage() && _moving)
            return _active != null ? Mathf.Max(0.1f, _active.walkFramesPerSecond) : 8f;

        if (_action == ActionKind.AutoAttack)
            return _actionFps;

        if (_active == null)
            return 8f;

        return Mathf.Max(0.1f, _moving ? _active.walkFramesPerSecond : _active.idleFramesPerSecond);
    }

    private Sprite[] GetCurrentFrames()
    {

        if (_action == ActionKind.Skill && _actionFrames != null && _actionFrames.Length > 0)
            return _actionFrames;

        if (IsMage() && _moving && _walkFrames.Length > 0)
            return _walkFrames;

        if (_action == ActionKind.AutoAttack && _actionFrames != null && _actionFrames.Length > 0)
            return _actionFrames;

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
