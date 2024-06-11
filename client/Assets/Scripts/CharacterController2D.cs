using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public sealed class CharacterController2D : MonoBehaviour
{
    [SerializeField]
    [Range(0, 5)]
    float _moveTime = .2f;

    [SerializeField]
    [Range(0, 5)]
    float _maxSpeed = 2f;

    Rigidbody2D _rigidbody;
    Animator _animator;
    CancellationTokenSource _moveCancellationTokenSource;
    Vector2 _velocity;

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    public void SmoothMoveTo(Vector2 targetPosition)
    {
        if (_moveCancellationTokenSource is { } cts)
            cts.Cancel();
        _moveCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        _ = MoveToTargetAsync(targetPosition, _moveCancellationTokenSource.Token);
    }

    async ValueTask MoveToTargetAsync(Vector2 targetPosition, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        var nowPos = (Vector2)gameObject.transform.position;
        TriggerMoveAnime(targetPosition - nowPos);
        while (Vector2.Distance(targetPosition, (Vector2)gameObject.transform.position) > .05f)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var smoothVelocity = Vector2.SmoothDamp(_rigidbody.position, targetPosition, ref _velocity, _moveTime, _maxSpeed);
            _rigidbody.velocity = (smoothVelocity - _rigidbody.position) / Time.deltaTime;
            await Task.Yield();
        }
        if (cancellationToken.IsCancellationRequested)
            return;

        _rigidbody.velocity = Vector2.zero;
        gameObject.transform.position = targetPosition;
        _animator.SetTrigger("idel");
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
