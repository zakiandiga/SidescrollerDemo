using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(InputHandler))]
public class Player : MonoBehaviour, IDamageHandler, IAttackHandler
{
    #region StateMachine Properties
    public PlayerStateMachine StateMachine { get; private set; }
    public PlayerIdle IdleState { get; private set; }
    public PlayerMove MoveState { get; private set; }
    public PlayerFall FallState { get; private set; }
    public PlayerJump JumpState { get; private set; }
    public PlayerLand LandState { get; private set; }
    public PlayerNormalAttack NormalAttackState { get; private set; }
    public PlayerAirNormalAttack AirNormalAttackState { get; private set; }
    public PlayerDashNormalAttack DashNormalAttackState { get; private set; }
    public PlayerTakeDamage takeDamageState { get; private set; }
    public PlayerDies dieState { get; private set; }
    #endregion

    #region Movement Calculation Properties
    public Vector3 PlayerVelocity { get { return playerVelocity; } private set { } }
    public float RawHorizontalVelocity { get { return rawPlayerVelocity.x; } private set { } }
    public float CurrentAngle { get { return playerRotation.y; } private set { } }
    public int JumpCount { get { return jumpCount; } private set { } }
    public bool IsGrounded { get { return GroundCheck(); } private set { } }
    #endregion

    #region Player Stats Properties
    public Vector3 PlayerPosition { get { return transform.position; } private set { } }
    public int PlayerMaxHP { get { return playerData.maxHP; } private set { } }
    public int PlayerCurrentHP { get { return _currentHP; } private set { } }
    public int CurrentDamage { get { return _currentDamage; } private set { } }
    public GameObject Weapon { get; private set; }
    #endregion

    #region Moving Components
    public Animator Anim { get; private set; }
    public InputHandler InputHandler { get; private set; }
    private CharacterController control;
    private EventBasedParticle eventParticle;
    #endregion

    #region Movement Calculation Variables
    [SerializeField] private Transform playerPivot;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private PlayerAnimationHolder playerAnimation;
    private Vector3 playerVelocity; //for final move/position calculation within this script, update the CurrentVelocity property to be read by other scripts
    private Vector3 rawPlayerVelocity;
    private Vector3 playerRotation;
    private float rawPlayerHorizontalVelocity;
    private float rawPlayerVerticalVelocity;
    private float playerGravity;
    private int jumpCount = 0;
    #endregion



    #region Player Stats Variable
    private int _currentHP;
    private int _currentDamage;
    [SerializeField] private Weapon _currentWeapon;
    #endregion

    #region Groundcheck Variables
    [SerializeField] private Transform groundChecker;
    [SerializeField] private float checkerRadius;
    [SerializeField] private LayerMask checkerMask;
    #endregion

    //DEBUG VARIABLES
    private float debugVelocity;

    #region Events
    public static event Action<Player> OnInitializePlayerUI;
    public event Action<int, Vector3, WeaponType> OnTakeDamage;
    public event Action<GameObject, Vector3> OnDies;
    public event Action<GameObject, Vector3> OnClearingCorpse;
    public event Action<Player> OnPlayerLanding;
    public static event Action<GameObject> OnPlayerDies;
    #endregion

    #region Initialization
    private void Awake()
    {
        
        SetStateMachine();
    }

    private void SetStateMachine()
    {
        StateMachine = new PlayerStateMachine();
        IdleState = new PlayerIdle(this, StateMachine, playerData, playerAnimation);
        MoveState = new PlayerMove(this, StateMachine, playerData, playerAnimation);
        FallState = new PlayerFall(this, StateMachine, playerData, playerAnimation);
        JumpState = new PlayerJump(this, StateMachine, playerData, playerAnimation);
        LandState = new PlayerLand(this, StateMachine, playerData, playerAnimation);
        NormalAttackState = new PlayerNormalAttack(this, StateMachine, playerData, playerAnimation);
        AirNormalAttackState = new PlayerAirNormalAttack(this, StateMachine, playerData, playerAnimation);
        DashNormalAttackState = new PlayerDashNormalAttack(this, StateMachine, playerData, playerAnimation);
        takeDamageState = new PlayerTakeDamage(this, StateMachine, playerData, playerAnimation);
        dieState = new PlayerDies(this, StateMachine, playerData, playerAnimation);    
    }

    private void Start()
    {
        Anim = GetComponentInChildren<Animator>();
        InputHandler = GetComponent<InputHandler>();
        control = GetComponent<CharacterController>();
        eventParticle = GetComponent<EventBasedParticle>();

        StateMachine.Initialize(IdleState);

        _currentHP = PlayerMaxHP;
        //playerVelocity = rawPlayerVelocity;
    }
    #endregion

    private void OnEnable()
    {
        PlayerHUD.OnPlayerUIActive += InitializePlayerUI;
    }

    private void OnDisable()
    {
        PlayerHUD.OnPlayerUIActive -= InitializePlayerUI;
    }

    private void InitializePlayerUI(PlayerHUD UI)
    {
        OnInitializePlayerUI?.Invoke(this);
    }

    private void Update()
    {
        /*
         *Quick test for taking damage
         *if(InputHandler.SpecialAttack)
         *{
         *TakeDamage(1, this.transform.position, WeaponType.none);
         *}
        */

        StateMachine.CurrentState.LogicUpdate();

        playerPivot.localEulerAngles = playerRotation;

        control.Move(playerVelocity * Time.deltaTime);

        if (transform.position.z != 0)
            transform.position = new Vector3 (transform.position.x, transform.position.y, 0);

    }

    private void LateUpdate()
    {
        StateMachine.CurrentState.PhysicsUpdate();
    }

    #region Check & Set Functions
    private bool GroundCheck()
    {
        return Physics.CheckSphere(groundChecker.position, checkerRadius, checkerMask);
    }

    public void SetVelocityX(float horizontalVelocity, float speedModifier)
    {
        rawPlayerVelocity.x = horizontalVelocity;
        playerVelocity.x = rawPlayerVelocity.x * speedModifier;
    }
    public void SetVelocityY(float verticalVelocity) => playerVelocity.y = verticalVelocity;

    public void AddJumpCount(int count) => jumpCount += count;

    public void ResetJumpCount() => jumpCount = 0;

    public void SetPlayerAngle(float angle) => playerRotation.y = angle;

    public void SetLandingEvent() => OnPlayerLanding?.Invoke(this);
    #endregion

    #region Interface Implementation
    public void SetCurrentDamage(int damage)
    {
        _currentDamage = damage;
    }

    public int GetCurrentDamage()
    {
        return _currentDamage;
    }

    public void TakeDamage(int damage, Vector3 contactPoint, WeaponType weaponType)
    {
        _currentHP -= damage;

        OnTakeDamage?.Invoke(damage, contactPoint, weaponType);

        if (_currentHP <= 0)
            Die();           
    }

    public void Die()
    {
        //Player dies!
        control.enabled = false;
        OnDies?.Invoke(this.gameObject, this.transform.position);
    }
    #endregion

    #region GroundCheck Visual Guide
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(groundChecker.position, checkerRadius);
    }
    #endregion
}
