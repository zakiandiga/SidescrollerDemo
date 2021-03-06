using System;
using UnityEngine;
using UnityEngine.AI;
using SGoap;

public class EnemyBehaviour : MonoBehaviour, IDamageHandler, IAttackHandler
{
    public int MaxHealth { get { return _maxHealth; } private set { } }
    public int CurrentHealth { get { return _currentHealth; } private set { } }

    public int MaxStamina { get { return _maxStamina; } private set { } }
    public float CurrentStamina { get { return _currentStamina; } private set { } }

    public int StaggerTreshold { get { return _staggerTreshold; } private set { } }

    public Vector3 EnemyPosition { get { return transform.position; } private set { } }
    public float MaxWanderRange { get { return enemyData.maxWanderRange; } private set { } }
    public float MinWanderRange { get { return enemyData.minWanderRange; } private set { } }
    public float WanderSpeed { get { return enemyData.wanderSpeed; } private set { } }
    public float AggroSpeed { get { return enemyData.aggroSpeed; } private set { } }
    public float AggroAlertColliderRadius { get { return enemyData.aggroAlertColliderRadius; } private set { } }
    public float WanderAlertColliderRadius { get { return enemyData.wanderAlertColliderRadius; } private set { } }
    public float RestAlertColliderRadius { get { return enemyData.restAlertColliderRadius; } private set { } }
    public float UnaggroDelay { get { return enemyData.unaggroDelay; } private set { } }

    public Transform currentPlayer { get; private set; }    
    public NavMeshPath AgentPath { get { return navAgent.path; } private set { } }
    public EnemyAnimationManager AnimManager { get; private set; }

    #region private Components
    private NavMeshAgent navAgent;
    private CapsuleCollider enemyCollider;
    private SphereCollider playerSensorCollider;
    private Agent goapAgent;
    [SerializeField] private EnemyData enemyData;
    [SerializeField] private GameObject alertCollider;
    private EventBasedParticle eventParticle;
    #endregion

    #region private Variables
    private int _maxHealth;
    private int _currentHealth;
    private int _maxStamina;
    [SerializeField] private float _currentStamina;
    private int _staggerTreshold;
    private int staggerCount;
    private int staggerTolerance;
    private int _currentDamage;

    private IDamageHandler playerDamageHandler;

    private float corpseCleaningTime => UnityEngine.Random.Range(4.5f, 6f);

    #region Particle Related Variables
    [SerializeField] private Transform poofLocation;
    #endregion

    //Timer string(s)
    private string aggroColliderTimer = "PlayerExitAggro";
    private string clearingEnemyTimer = "EnemyDead";

    #endregion

    public enum EnemyType //variation in behavior data
    {
        neutral,
        hostile
    }

    [SerializeField] private EnemyType enemyType = EnemyType.neutral;

    #region Events
    //Interface events
    public event Action<int, Vector3, WeaponType> OnTakeDamage;
    public event Action<GameObject, Vector3> OnDies;
    public event Action<GameObject, Vector3> OnClearingCorpse;

    //OnInitialize Events
    public static event Action<EnemyBehaviour> OnEnemySpawn;

    //Events to be reviewed
    public static event Action<GameObject, int> OnEnemyTakesDamage;
    public static event Action<GameObject> OnEnemyDies;
    public static event Action<GameObject> OnEnemyStagger;
    #endregion

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        goapAgent = GetComponent<Agent>();
        AnimManager = GetComponent<EnemyAnimationManager>();
        enemyCollider = GetComponent<CapsuleCollider>();
        playerSensorCollider = alertCollider.GetComponent<SphereCollider>();
        eventParticle = GetComponent<EventBasedParticle>();       
    }

    private void OnEnable()
    {
        //basic stats initiation
        if (!enemyCollider.enabled)
            enemyCollider.enabled = true;

        _maxHealth = enemyData.maxHP;
        if (_currentHealth <= 0)
            _currentHealth = _maxHealth;

        _maxStamina = enemyData.maxStamina;
        if (_currentStamina == 0)
        {
            _currentStamina = _maxStamina;
            if (!goapAgent.States.HasState("CurrentStamina"))
                goapAgent.States.AddState("CurrentStamina", _currentStamina);
            else
                goapAgent.States.SetState("CurrentStamina", _currentStamina);
        }

        if (goapAgent.States.HasState("IsDead"))
        {
            goapAgent.States.RemoveState("IsDead");
        }

        _staggerTreshold = enemyData.staggerTrashold;

        OnEnemySpawn?.Invoke(this);
    }

    private void Update()
    {  
        if (_currentStamina < _maxStamina)
        {
            StaminaRegen();
        }
    }

    private void StaminaRegen()
    {
        _currentStamina += enemyData.staminaRegenerationRate * Time.deltaTime;
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
        goapAgent.States.SetState("CurrentStamina", _currentStamina);
    }

    //Amount of stamina spent after executing specific attack (Called from the attack script)
    public void StaminaUsed(float value)
    {
        _currentStamina -= value;
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
        goapAgent.States.SetState("CurrentStamina", _currentStamina);
    }

    #region Player Sensor
    //Check whether this enemy is currently in aggro state
    public bool IsAlert() => goapAgent.States.HasState("IsAlert");

    //Set the alert collider radius from the inspector
    public void SetAlertColliderRadius(float targetRadius) => playerSensorCollider.radius = targetRadius;

    //Set the current target player when this enemy turns aggro
    public void SetPlayerOn(Transform player)
    {
        if (!IsAlert())
        {
            currentPlayer = player;
            playerDamageHandler = currentPlayer.GetComponent<IDamageHandler>();
            playerDamageHandler.OnDies += PlayerDieHandler;

            goapAgent.States.AddState("IsAlert", 1);            
        }
        else        
            Timer.ForceStopTimer(aggroColliderTimer);
    }

    public void SetPlayerOff(Transform player) => Timer.Create(PlayerExitAggro, UnaggroDelay, aggroColliderTimer);

    private void PlayerExitAggro()
    {
        playerDamageHandler.OnDies -= PlayerDieHandler;
        playerDamageHandler = null;
        currentPlayer = null;
        goapAgent.States.RemoveState("IsAlert");
    }

    private void PlayerDieHandler(GameObject player, Vector3 position) => PlayerExitAggro();
    #endregion

    #region NavMesh Movement Functions
    public void SetLookAt(Vector3 lookTarget)
    {
        if (currentPlayer != null)
        {
            navAgent.updateRotation = false;
            transform.LookAt(currentPlayer.position);
            navAgent.updateRotation = true;
        }
    }

    public void SetDestination(Vector3 destination, float speed)
    {
        navAgent.speed = speed;      
        navAgent.SetDestination(destination);
    }

    public void SetStopNavMesh() => navAgent.ResetPath();

    public float NavRemainingDistance() => navAgent.remainingDistance;

    public float NavVelocity() => Mathf.Abs(navAgent.velocity.x);
    #endregion

    #region Interface implementations
    public void SetCurrentDamage(int damage)
    {
        _currentDamage = damage;
    }

    public int GetCurrentDamage()
    {
        return _currentDamage;
    }

    public void Die()
    {
        AnimManager.SetDead();
        enemyCollider.enabled = false;
        OnEnemyDies?.Invoke(this.gameObject);
        OnDies?.Invoke(this.gameObject, this.transform.position);
        Debug.Log("Enemy dies");
        goapAgent.States.SetState("IsDead", 1);
        Timer.Create(ClearEnemy, corpseCleaningTime, clearingEnemyTimer);

    }

    public void ClearEnemy()
    {
        //poof VFX
        OnClearingCorpse?.Invoke(this.gameObject, poofLocation.position);
    }

    public void TakeDamage(int damage, Vector3 contactPoint, WeaponType weaponType)
    {
        _currentHealth -= damage;
        _staggerTreshold -= damage;

        OnTakeDamage?.Invoke(damage, contactPoint, weaponType);

        if (_currentHealth <= 0)
            Die();

        if (_staggerTreshold <= 0)
        {
            Stagger();
        }
        
        OnEnemyTakesDamage?.Invoke(this.gameObject, damage);
    }
    #endregion

    public void Stagger()
    {
        staggerCount += 1;
        _staggerTreshold = enemyData.staggerTrashold;
        OnEnemyStagger?.Invoke(this.gameObject);
    }
}
