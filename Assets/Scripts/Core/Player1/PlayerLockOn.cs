using UnityEngine;

public class PlayerLockOn : MonoBehaviour
{
    [Header("Lock-On Search")]
    public float lockRadius = 12f;
    public string enemyTag = "Enemy";

    [Header("Indicator")]
    public GameObject lockIndicatorPrefab;

    [Header("Runtime")]
    public LockOnTarget currentTarget;

    private GameObject currentIndicatorInstance;

    void Awake()
    {
        
        currentTarget = null;

        if (currentIndicatorInstance != null)
        {
            Destroy(currentIndicatorInstance);
            currentIndicatorInstance = null;
        }
    }

    void OnDisable()
    {
        ClearLockTarget();
    }

    void Update()
    {
        ValidateCurrentTarget();
        HandleLockInput();
    }

    void HandleLockInput()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            // 先再检查一次，防止死目标卡住
            ValidateCurrentTarget();

            if (!HasTarget())
            {
                TryLockNearestTarget();
            }
            else
            {
                ClearLockTarget();
            }
        }
    }

    void ValidateCurrentTarget()
    {
        if (currentTarget == null)
        {
            currentTarget = null;
            return;
        }

        if (!currentTarget.CanBeLocked())
        {
            ClearLockTarget();
        }
    }

    void TryLockNearestTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);

        LockOnTarget nearest = null;
        float nearestSqrDist = float.MaxValue;
        Vector3 playerPos = transform.position;

        foreach (GameObject enemyObj in enemies)
        {
            if (enemyObj == null) continue;

            LockOnTarget target = enemyObj.GetComponent<LockOnTarget>();
            if (target == null) continue;
            if (!target.CanBeLocked()) continue;

            float sqrDist = (enemyObj.transform.position - playerPos).sqrMagnitude;
            if (sqrDist > lockRadius * lockRadius) continue;

            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = target;
            }
        }

        if (nearest != null)
        {
            SetLockTarget(nearest);
        }
    }

    void SetLockTarget(LockOnTarget target)
    {
        if (target == null) return;
        if (!target.CanBeLocked()) return;

        ClearLockTarget();

        currentTarget = target;
        currentTarget.OnLockTargetDied += HandleTargetDied;

        CreateIndicator();
    }

    void HandleTargetDied(LockOnTarget deadTarget)
    {
        if (currentTarget == deadTarget)
        {
            ClearLockTarget();
        }
    }

    public void ClearLockTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.OnLockTargetDied -= HandleTargetDied;
        }

        currentTarget = null;

        if (currentIndicatorInstance != null)
        {
            Destroy(currentIndicatorInstance);
            currentIndicatorInstance = null;
        }
    }

    void CreateIndicator()
    {
        if (currentTarget == null) return;
        if (lockIndicatorPrefab == null) return;

        Transform head = currentTarget.GetHeadPoint();
        currentIndicatorInstance = Instantiate(lockIndicatorPrefab, head);
        currentIndicatorInstance.transform.localPosition = Vector3.zero;
        currentIndicatorInstance.transform.localRotation = Quaternion.identity;
    }

    public bool HasTarget()
    {
        return currentTarget != null && currentTarget.CanBeLocked();
    }

    public Transform GetTargetTransform()
    {
        return HasTarget() ? currentTarget.transform : null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lockRadius);
    }
}