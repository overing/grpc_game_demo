using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public sealed class CharacterController2D : MonoBehaviour
{
    [SerializeField]
    TextMesh _nameTextMesh;

    [SerializeField]
    [Range(0, 5)]
    float _moveTime = .2f;

    [SerializeField]
    [Range(0, 5)]
    float _maxSpeed = 2f;

    Rigidbody2D _rigidbody;
    Animator _animator;
    Coroutine _moveCodoutine;
    Vector2 _velocity;

    void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    public void SetName(string name) => _nameTextMesh.text = name;

    public void SmoothMoveTo(Vector2 targetPosition)
    {
        if (_moveCodoutine is { } exists)
            StopCoroutine(exists);
        _moveCodoutine = StartCoroutine(MoveToTargetAsync(targetPosition));
    }

    IEnumerator MoveToTargetAsync(Vector2 targetPosition)
    {
        var nowPos = (Vector2)gameObject.transform.position;
        TriggerMoveAnime(targetPosition - nowPos);
        while (Vector2.Distance(targetPosition, (Vector2)gameObject.transform.position) > .05f)
        {
            var smoothVelocity = Vector2.SmoothDamp(_rigidbody.position, targetPosition, ref _velocity, _moveTime, _maxSpeed);
            _rigidbody.velocity = (smoothVelocity - _rigidbody.position) / Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _rigidbody.velocity = Vector2.zero;
        gameObject.transform.position = targetPosition;
        _animator.SetTrigger("idle");
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
