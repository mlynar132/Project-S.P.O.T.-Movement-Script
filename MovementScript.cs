using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerMovement : MonoBehaviour
{
    [Header("Script set up")]
    [SerializeField] private Animator anim;
    [SerializeField] private PlayerLogic _playerLogic;
    [SerializeField] private GameObject _cameraRig;
    [SerializeField] Rigidbody _rb;
    [SerializeField] private LayerMask _whatIsGround;
    private PlayerControls _playerControls;
    [SerializeField] Collider _standingCollider;
    [SerializeField] Collider _crouchingCollider;

    [Header("Movement speed and all that good stuff")]
    [SerializeField] private float _currentMoveSpeed;
    [SerializeField] private float _WalkSpeed;
    [SerializeField] private float _SprintSpeed;
    [SerializeField] private float _sprintMultiplier;
    [SerializeField] private bool _isSprinting;
    [SerializeField] private float _desiredMoveSpeed;
    [SerializeField] private float _lastDesiredMoveSpeed;
    [SerializeField] private float _speedIncreaseMultiplayer; //this is for interpolation

    [Header("Jumping stuff")]
    [SerializeField] private float _jumpForce;
    [SerializeField] private float _jumpCooldown;
    [SerializeField] private float _airMultiplier;
    [SerializeField] private bool _readyToJump;
    [SerializeField] private bool _isJumping;

    [Header("Crouching and sliding")]
    [SerializeField] private float _crouchSpeed;
    [SerializeField] private float _slideSpeed;
    [SerializeField] private float _maxSlideSpeed;
    [SerializeField] private float _minSlideSpeed;
    [SerializeField] private float _accelerationRateOnSlope;
    [SerializeField] private float _DecelerationRateNotSlope;
    [SerializeField] private float _crouchYScale;
    [SerializeField] private float _startYScale;

    [Header("Slope stuff")]
    [SerializeField] private float _maxSlopeAngle;
    [SerializeField] private RaycastHit _slopeHit;
    [SerializeField] private bool _exitingSlope;
    [SerializeField] private float _slopeIncreaseMultiplier;

    [Header("Drags based on material")]
    [SerializeField] private float _dragGround;
    [SerializeField] private float _dragWall;
    [SerializeField] private float _dragAir;

    [Header("Checking for ground and walls")]
    [SerializeField] private float _groundCheckLength;

    [Header("Debuging purposes")]
    [SerializeField] public MovementState State;
    [SerializeField] private bool _isMoving;
    [SerializeField] private bool _isGrounded;
    [SerializeField] private bool _isRunning;
    [SerializeField] private bool _isSliding;
    [SerializeField] private float _crouchPushDownForce;
    private Transform _orientation;
    private Vector3 _moveDirection;

    private AudioSource source;
    //JumpSound
    [Header("PlayerSfx")]
    [Range(0.1f, 0.5f)]
    public float jumpSFXVolume = 0.2f;
    [Range(0.1f, 0.5f)]
    public float jumpSFXPitch = 0.2f;
    [SerializeField] private AudioClip[] jumpSounds;

    //WalkSound
    [Range(0.1f, 0.5f)]
    public float walkSFXVolume = 0.2f;
    [Range(0.1f, 0.2f)]
    public float walkSFXPitch = 0.02f;
    public AudioClip[] walkSounds;

    //SlideSound
    [Range(0.8f, 1f)]
    public float slideSFXVolume = 0.2f;
    [Range(0.95f, 1f)]
    public float slideSFXPitch = 0.2f;
    public AudioClip[] slideSounds;

    /*[Header("Animations")]
     [SerializeField]*/
    [Header("Player rotation")]
    [SerializeField] GameObject _model;

    public enum MovementState
    {
        idle,
        walking,
        sprinting,
        crouching,
        sliding,
        jump,
        airAscending,
        airDescending,
    }

    private void Start()
    {

        _readyToJump = true;
        _exitingSlope = false;
        _startYScale = transform.localScale.y;//animation wil probably make this irrelevant
        //_currentMoveSpeed = _playerLogic.MovementSpeed;
        _playerControls = new PlayerControls();
        _playerControls.Player.Move.Enable();
        _playerControls.Player.Move.performed += Move;
        _playerControls.Player.Sprint.Enable();
        _playerControls.Player.Sprint.performed += Sprint;
        _playerControls.Player.Crouch.Enable();
        _playerControls.Player.Crouch.performed += Crouch;
        _playerControls.Player.Crouch.canceled += Crouch;
        _playerControls.Player.Jump.Enable();
        _playerControls.Player.Jump.performed += Jump;
        _playerControls.Player.Throw.Enable();
        _playerControls.Player.Throw.performed += Throw;

        source = GetComponent<AudioSource>();

    }
    private void Update()
    {
        _isGrounded = Physics.Raycast(transform.position + new Vector3(0f, 0.1f, 0f), Vector3.down, _groundCheckLength, _whatIsGround);
        SpeedControl();
        StateHandler();
        if (_isGrounded)
        {
            _rb.drag = _dragGround;
        }
        else
        {
            _rb.drag = _dragAir;
        }
    }
    private void FixedUpdate()
    {
        MovePlayer();
        RotatePlayer();
    }
    private void StateHandler()
    {
        if (_isGrounded)
        {
            anim.SetBool("Grounded", true);
            //might be a problem with sprinting and crouchiing at once
            if (_playerControls.Player.Crouch.ReadValue<float>() == 1)
            {
                _crouchingCollider.enabled = true;
                _standingCollider.enabled = false;
                if (_rb.velocity.magnitude >= _minSlideSpeed)
                {
                    _slideSpeed = _currentMoveSpeed;
                    _desiredMoveSpeed = _slideSpeed;
                    State = MovementState.sliding;
                    anim.SetBool("Sliding", true);
                    anim.SetBool("Ascending", false);
                    anim.SetBool("Descending", false);
                    anim.SetBool("Idle", false);
                    anim.SetBool("Walking", false);
                    anim.SetBool("Running", false);
                    anim.SetBool("Crouching", false);
                }
                else
                {
                    _desiredMoveSpeed = _crouchSpeed;
                    State = MovementState.crouching;
                    anim.SetBool("Crouching", true);
                    anim.SetBool("Ascending", false);
                    anim.SetBool("Descending", false);
                    anim.SetBool("Idle", false);
                    anim.SetBool("Walking", false);
                    anim.SetBool("Running", false);
                    anim.SetBool("Sliding", false);
                }
            }
            else if (!Physics.Raycast(transform.position + new Vector3(0f, 1f, 0f), Vector3.up, 4f, _whatIsGround)) // check if you need to force crouch
            {
                if (_playerControls.Player.Sprint.ReadValue<float>() == 1 && _playerControls.Player.Move.ReadValue<Vector2>().magnitude != 0)
                {
                    _crouchingCollider.enabled = false;
                    _standingCollider.enabled = true;
                    State = MovementState.sprinting;
                    _desiredMoveSpeed = _SprintSpeed;
                    anim.SetBool("Running", true);
                    anim.SetBool("Ascending", false);
                    anim.SetBool("Descending", false);
                    anim.SetBool("Idle", false);
                    anim.SetBool("Walking", false);
                    anim.SetBool("Crouching", false);
                    anim.SetBool("Sliding", false);
                }
                else if (_playerControls.Player.Move.ReadValue<Vector2>().magnitude != 0)
                {
                    _crouchingCollider.enabled = false;
                    _standingCollider.enabled = true;
                    State = MovementState.walking;
                    _desiredMoveSpeed = _WalkSpeed;
                    anim.SetBool("Walking", true);
                    anim.SetBool("Ascending", false);
                    anim.SetBool("Descending", false);
                    anim.SetBool("Idle", false);
                    anim.SetBool("Running", false);
                    anim.SetBool("Crouching", false);
                    anim.SetBool("Sliding", false);
                }
                else
                {
                    _crouchingCollider.enabled = false;
                    _standingCollider.enabled = true;
                    State = MovementState.idle;
                    _desiredMoveSpeed = 0f;
                    anim.SetBool("Idle", true);
                    anim.SetBool("Ascending", false);
                    anim.SetBool("Descending", false);
                    anim.SetBool("Walking", false);
                    anim.SetBool("Running", false);
                    anim.SetBool("Crouching", false);
                    anim.SetBool("Sliding", false);
                }
            }
        }
        else
        {
            _crouchingCollider.enabled = false;
            _standingCollider.enabled = true;
            anim.SetBool("Grounded", false);
            if (_currentMoveSpeed < _WalkSpeed)
            {
                _desiredMoveSpeed = _WalkSpeed;
            }
            if (_rb.velocity.y >= 0.1f)
            {
                State = MovementState.airAscending;
                anim.SetBool("Ascending", true);
                anim.SetBool("Descending", false);
                anim.SetBool("Idle", false);
                anim.SetBool("Walking", false);
                anim.SetBool("Running", false);
                anim.SetBool("Crouching", false);
                anim.SetBool("Sliding", false);
            }
            else if (_rb.velocity.y <= -0.1f)
            {
                State = MovementState.airDescending;
                anim.SetBool("Descending", true);
                anim.SetBool("Ascending", false);
                anim.SetBool("Idle", false);
                anim.SetBool("Walking", false);
                anim.SetBool("Running", false);
                anim.SetBool("Crouching", false);
                anim.SetBool("Sliding", false);
            }
        }
        //interpolate the movement speed when the change is higer than than from walk to speed
        if (Mathf.Abs(_desiredMoveSpeed - _lastDesiredMoveSpeed) > _SprintSpeed - _WalkSpeed && _currentMoveSpeed != 0)
        {
            // StopCoroutine(nameof(SmoothlyLerpMoveSpeed));
            //  StartCoroutine(nameof(SmoothlyLerpMoveSpeed));
        }
        else
        {
            //  StopCoroutine(nameof(SmoothlyLerpMoveSpeed));

        }
        _currentMoveSpeed = _desiredMoveSpeed;
        _lastDesiredMoveSpeed = _currentMoveSpeed;
        //AnimOFF();
    }
    /* private void AnimationOFF(string current)
     {
         anim.SetBool(current, );
     }*/
    private void AnimOFF() //make it with previous state and current one so previous is false and current is true
    {
        anim.SetBool("Ascending", false);
        anim.SetBool("Descending", false);
        anim.SetBool("Grounded", false);
        anim.SetBool("Idle", false);
        anim.SetBool("Walking", false);
        anim.SetBool("Running", false);
        anim.SetBool("Crouching", false);
        anim.SetBool("Sliding", false);
    }
    private void MovePlayer()
    {
        //_moveDirection should be normalized due to the new input system
        _moveDirection = new Vector3(_playerControls.Player.Move.ReadValue<Vector2>().x, 0, _playerControls.Player.Move.ReadValue<Vector2>().y);
        _moveDirection = _cameraRig.transform.TransformDirection(_moveDirection);
        _moveDirection.y = 0f;

        /*_moveDirection = transform.TransformPoint(transform.forward * _playerControls.Player.Move.ReadValue<Vector2>().y) + transform.TransformPoint(transform.right * _playerControls.Player.Move.ReadValue<Vector2>().x);
        Debug.Log(transform.forward * _playerControls.Player.Move.ReadValue<Vector2>().y + " "+transform.TransformPoint(transform.forward * _playerControls.Player.Move.ReadValue<Vector2>().y));*/

        //slope
        if (OnSlope() && !_exitingSlope)
        {
            if (State == MovementState.sliding)
            {
                SlideMovement();
            }
            else
            {
                _rb.AddForce(GetSlopeMoceDirection() * _currentMoveSpeed * 10f, ForceMode.Force); //check with 20f
                if (_rb.velocity.y > 0.1)
                {
                    _rb.AddForce(Vector3.down * 10f, ForceMode.Force);
                }
            }
        }
        //flat ground
        else if (_isGrounded)
        {
            if (State == MovementState.sliding)
            {
                SlideMovement();
            }
            else
            {
                _rb.AddForce(_moveDirection * _currentMoveSpeed * 10f, ForceMode.Force);
            }
        }
        //air
        else
        {
            _rb.AddForce(_moveDirection * _currentMoveSpeed * 10f * _airMultiplier, ForceMode.Force);

        }
        _rb.useGravity = !OnSlope();
    }
    private void SpeedControl()
    {
        if (OnSlope() && !_exitingSlope)
        {

            if (_rb.velocity.magnitude > _currentMoveSpeed)
            {
                _rb.velocity = _rb.velocity.normalized * _currentMoveSpeed;
            }
        }
        else
        {
            Vector3 horizontalVelocity = new Vector3(_rb.velocity.x, 0, _rb.velocity.z);
            if (horizontalVelocity.magnitude > _currentMoveSpeed)
            {
                Vector3 capedVelocity = horizontalVelocity.normalized * _currentMoveSpeed;
                _rb.velocity = new Vector3(capedVelocity.x, _rb.velocity.y, capedVelocity.z);
            }
        }
    }
    private void RotatePlayer()
    {
        if (_playerControls.Player.Move.ReadValue<Vector2>().magnitude != 0)
        {
            Quaternion rotation;
            if (OnSlope())
            {
                Debug.Log("ASD");
                rotation = Quaternion.LookRotation(GetSlopeMoceDirection(), Vector3.up);
            }
            else
            {
                rotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
            }

            _model.transform.rotation = rotation;
        }

    }
    private void Move(InputAction.CallbackContext context)
    {

    }
    private void Sprint(InputAction.CallbackContext context)
    {

    }
    private void Crouch(InputAction.CallbackContext context)
    {
        /* transform.localScale = new Vector3(transform.localScale.x, _crouchYScale, transform.localScale.z);
         _rb.AddForce(Vector3.down * _crouchPushDownForce, ForceMode.Impulse);*/
        /* if (context.canceled)
         {
             transform.localScale = new Vector3(transform.localScale.x, _startYScale, transform.localScale.z);
         }*/
    }
    private void Jump(InputAction.CallbackContext context)
    {
        //maybe do it with get key so it's constantly cheking for it
        if (_isGrounded && _readyToJump)
        {
            State = MovementState.jump;
            anim.SetTrigger("Jump");
            _exitingSlope = true;
            _rb.velocity = new Vector3(_rb.velocity.x, 0, _rb.velocity.z);
            _rb.AddForce(transform.up * _jumpForce, ForceMode.Impulse);
            _readyToJump = false;

            Invoke(nameof(JumpReset), _jumpCooldown);
        }
    }
    private void JumpReset()
    {
        _readyToJump = true;
        _exitingSlope = false;
    }
    private void Throw(InputAction.CallbackContext context)
    {

    }
    private void StartSlide()
    {
        _isSliding = true;
    }
    private void SlideMovement()
    {
        _moveDirection = new Vector3(_playerControls.Player.Move.ReadValue<Vector2>().x, 0, _playerControls.Player.Move.ReadValue<Vector2>().y);
        _moveDirection = _cameraRig.transform.TransformDirection(_moveDirection);
        _moveDirection.y = 0f;
        if (OnSlope())
        {
            if (_rb.velocity.y <= 0.1f)//downwords
            {
                float angle = Vector3.Angle(Vector3.up, _slopeHit.normal);
                float angleIncrease = 1 + (angle / 90f);
                _slideSpeed += angleIncrease * 0.1f;
            }
            else//up
            {
                float angle = Vector3.Angle(Vector3.up, _slopeHit.normal);
                float angleIncrease = 1 + (angle / 90f);
                _slideSpeed -= angleIncrease * 0.1f; // we can do Decrease rate on slopees and increase rate on slopes
            }

        }
        else
        {
            _slideSpeed -= _DecelerationRateNotSlope;
        }
        _slideSpeed = Mathf.Clamp(_slideSpeed, 0, _maxSlideSpeed);
        _currentMoveSpeed = _slideSpeed;
        _rb.AddForce(GetSlopeMoceDirection() * _currentMoveSpeed * 10f, ForceMode.Force);
    }
    private void StopSlide()
    {
        _isSliding = false;
    }
    private bool OnSlope()
    {
        if (Physics.Raycast(transform.position + new Vector3(0f, 0.1f, 0f), Vector3.down, out _slopeHit, _groundCheckLength, _whatIsGround))
        {
            float angle = Vector3.Angle(Vector3.up, _slopeHit.normal);
            return angle < _maxSlopeAngle && angle != 0;
        }
        return false;
    }
    private Vector3 GetSlopeMoceDirection()
    {
        return Vector3.ProjectOnPlane(_moveDirection, _slopeHit.normal).normalized;
    }
    private IEnumerator SmoothlyLerpMoveSpeed()
    {
        float time = 0f;
        float difference = Mathf.Abs(_desiredMoveSpeed - _currentMoveSpeed);
        float startValue = _currentMoveSpeed;
        while (time < difference)
        {
            if (State == MovementState.sliding)
            {
                float angle = Vector3.Angle(Vector3.up, _slopeHit.normal);
                float angleIncrease = 1 + (angle / 90f);
                //make it slow down faster
                //make quick transition from sliding to walking and running
                time += Time.deltaTime * _speedIncreaseMultiplayer /* * _slopeIncreaseMultiplier * angleIncrease*/;
                _currentMoveSpeed = Mathf.Lerp(startValue, _desiredMoveSpeed, time / difference);
            }
            else
            {
                time += Time.deltaTime * _speedIncreaseMultiplayer;
                _currentMoveSpeed = Mathf.Lerp(startValue, _desiredMoveSpeed, time / difference);
            }

            yield return null;
        }
        _currentMoveSpeed = _desiredMoveSpeed;
    }
}
