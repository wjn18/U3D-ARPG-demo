using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(GuardAI))]
public class GuardAnimationController : MonoBehaviour
{
    [Header("Animator Params")]
    public string speedParam = "Speed";
    public string attackTrigger = "Attack";
    public string chooseParam = "Choose";
    public string isWaitingIdleParam = "IsWaitingIdle";

    [Header("Speed Smooth")]
    public float dampTime = 0.1f;

    [Header("Wait Idle")]
    public float waitIdleDelay = 5f;
    public float moveThreshold = 0.1f;

    private Animator anim;
    private GuardAI guardAI;
    private float idleTimer = 0f;
    private GuardAnimationConfig AnimationConfig => guardAI != null && guardAI.config != null && guardAI.config.animation != null
        ? guardAI.config.animation
        : null;

    void Awake()
    {
        anim = GetComponent<Animator>();
        guardAI = GetComponent<GuardAI>();
    }

    void OnEnable()
    {
        if (guardAI != null)
            guardAI.OnAction += HandleAction;
    }

    void OnDisable()
    {
        if (guardAI != null)
            guardAI.OnAction -= HandleAction;
    }

    void Update()
    {
        if (anim == null || guardAI == null) return;

        GuardAnimationConfig cfg = AnimationConfig;
        string speedName = cfg != null ? cfg.speedParam : speedParam;
        string waitingName = cfg != null ? cfg.isWaitingIdleParam : isWaitingIdleParam;
        float speedDamp = cfg != null ? cfg.dampTime : dampTime;
        float waitDelay = cfg != null ? cfg.waitIdleDelay : waitIdleDelay;
        float idleMoveThreshold = cfg != null ? cfg.moveThreshold : moveThreshold;

        float speed = guardAI.CurrentSpeed;
        anim.SetFloat(speedName, speed, speedDamp, Time.deltaTime);

        if (speed <= idleMoveThreshold)
            idleTimer += Time.deltaTime;
        else
            idleTimer = 0f;

        anim.SetBool(waitingName, idleTimer >= waitDelay);
    }

    void HandleAction(GuardActionData action)
    {
        if (anim == null) return;

        GuardAnimationConfig cfg = AnimationConfig;
        string chooseName = cfg != null ? cfg.chooseParam : chooseParam;
        string attackName = cfg != null ? cfg.attackTrigger : attackTrigger;
        string waitingName = cfg != null ? cfg.isWaitingIdleParam : isWaitingIdleParam;

        int choose = GetActionChooseValue(action);
        anim.SetInteger(chooseName, choose);
        anim.SetTrigger(attackName);

        idleTimer = 0f;
        anim.SetBool(waitingName, false);
    }

    int GetActionChooseValue(GuardActionData action)
    {
        if (action == null)
            return UnityEngine.Random.Range(0, 4);

        // Heal is wired in the Animator as Attack trigger + Choose == 1.
        if (action.actionType == GuardActionType.Heal)
            return 1;

        return action.animatorChooseValue;
    }
}
