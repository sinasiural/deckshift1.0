using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Distinct outcomes so callers (UI feedback later) can tell a conflict refusal
// apart from an ordinary gate failure. Blocked plays consume no charge.
public enum CardExecuteResult
{
    Success,
    Failed,   // gate check failed (e.g. Comet Dive while grounded) — pre-existing semantics
    Blocked,  // refused: the action's declared state overlaps a live effect's flags
}

public class CardActionExecutor : MonoBehaviour
{
    private PlayerController player;
    private Dictionary<CardActionType, CardAction> actions;

    // Tracks which coroutine-based actions are currently mid-execution.
    // Instant actions (IsCoroutine = false) are never added — they complete synchronously.
    // Note: a HashSet cannot hold duplicate references, so replaying a coroutine action
    // before the previous run completes will not double-count it in the set.
    private HashSet<CardAction> runningEffects = new HashSet<CardAction>();
    private ConflictFlags activeFlags = ConflictFlags.None;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        actions = new Dictionary<CardActionType, CardAction>
        {
            { CardActionType.Jump,           new JumpAction()           },
            { CardActionType.Dash,           new DashAction()           },
            { CardActionType.PlatformCreate, new PlatformCreateAction() },
            { CardActionType.Fireball,       new FireballAction()       },
            { CardActionType.Portal,         new PortalAction()         },
            { CardActionType.VampiricBite,   new VampiricBiteAction()   },
            { CardActionType.GlassWail,      new GlassWailAction()      },
            { CardActionType.Phase,          new PhaseAction()          },
            { CardActionType.CometDive,      new CometDiveAction()      },
            { CardActionType.Adrenaline,     new AdrenalineAction()     },
            { CardActionType.Stagger,        new StaggerAction()        },
            { CardActionType.ReverseGravity, new ReverseGravityAction() },
            { CardActionType.GlassParry,     new GlassParryAction()     },
            { CardActionType.DeadWeight,     new DeadWeightAction()     },
            { CardActionType.FreefallBlade,  new FreefallBladeAction()  },
            { CardActionType.ReturnAnchor,   new ReturnAnchorAction()   },
            { CardActionType.Shuriken,       new ShurikenAction()       },
            { CardActionType.SalvagedShuriken, new SalvagedShurikenAction() },
            { CardActionType.ThroughAndThrough, new ThroughAndThroughAction() },
        };
    }

    // Dispatches a card action.
    // keepCardInHand is set to true by Portal on its first click (card stays in hand).
    // BLOCK enforcement: if the action's declared ModifiedState overlaps any live
    // effect's flags, the action is refused before any of its code runs. Blocking is
    // uniform — same-card replays are refused too (design decision), which is why
    // ReverseGravity's timer-refresh branch is now unreachable (see PlayerController).
    public CardExecuteResult TryExecute(CardActionType type, float value, out bool keepCardInHand)
    {
        keepCardInHand = false;
        if (!actions.TryGetValue(type, out CardAction action)) return CardExecuteResult.Failed;

        if ((action.ModifiedState & activeFlags) != ConflictFlags.None)
            return CardExecuteResult.Blocked;

        if (action.IsCoroutine)
        {
            // Execute acts as a gate check for coroutine actions.
            if (!action.Execute(player, value, out keepCardInHand)) return CardExecuteResult.Failed;
            IEnumerator routine = action.ExecuteCoroutine(player, value);
            if (routine == null) return CardExecuteResult.Failed;
            StartCoroutine(ManagedCoroutine(action, routine));
            return CardExecuteResult.Success;
        }

        return action.Execute(player, value, out keepCardInHand)
            ? CardExecuteResult.Success
            : CardExecuteResult.Failed;
    }

    private IEnumerator ManagedCoroutine(CardAction action, IEnumerator inner)
    {
        runningEffects.Add(action);
        activeFlags |= action.ModifiedState;
        try
        {
            yield return StartCoroutine(inner);
        }
        finally
        {
            runningEffects.Remove(action);
            activeFlags &= ~action.ModifiedState;
        }
    }

    // For effects whose coroutines live outside the executor (e.g. Adrenaline, ReverseGravity).
    // Does not touch runningEffects — lifecycle is managed by the caller.
    public void SetManualFlag(ConflictFlags flags, bool active)
    {
        if (active) activeFlags |= flags;
        else        activeFlags &= ~flags;
    }

    public bool IsEffectActive(CardActionType type) =>
        actions.TryGetValue(type, out CardAction a) && runningEffects.Contains(a);

    public ConflictFlags ActiveFlags => activeFlags;
}
