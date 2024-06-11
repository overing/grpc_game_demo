using UnityEngine;

public sealed class CharacterController2D : MonoBehaviour
{
    [SerializeField]
    [Range(0, 5)]
    float _moveTime = .2f;

    [SerializeField]
    [Range(0, 5)]
    float _maxSpeed = 2f;

    [SerializeField]
    Vector2 _targetPos;

    [SerializeField]
    Vector2 _velocity;

    Rigidbody2D _rigidbody;
    Animator _animator;

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var mousePos = Input.mousePosition;
            mousePos = new Vector3(mousePos.x, mousePos.y, Camera.main.nearClipPlane);
            _targetPos = Camera.main.ScreenToWorldPoint(mousePos);
            var nowPos = (Vector2)gameObject.transform.position;
            TriggerMoveAnime(_targetPos - nowPos);
        }
    }

    void FixedUpdate()
    {
        var nowPos = (Vector2)gameObject.transform.position;
        if (Vector2.Distance(_targetPos, nowPos) > .05f)
        {
            var smoothVelocity = Vector2.SmoothDamp(_rigidbody.position, _targetPos, ref _velocity, _moveTime, _maxSpeed);
            _rigidbody.velocity = (smoothVelocity - _rigidbody.position) / Time.fixedDeltaTime;
        }
        else
        {
            _rigidbody.velocity = Vector2.zero;
            gameObject.transform.position = _targetPos;
            _animator.SetTrigger("idel");
        }
    }

    void TriggerMoveAnime(Vector2 moving)
    {
        if (Mathf.Abs(moving.x) > Mathf.Abs(moving.y))
        {
            if (moving.x > 0)
                _animator.SetTrigger("right");
            else if (moving.x < 0)
                _animator.SetTrigger("left");
        }
        else
        {
            if (moving.y > 0)
                _animator.SetTrigger("up");
            else if (moving.y < 0)
                _animator.SetTrigger("down");
        }
    }
}
