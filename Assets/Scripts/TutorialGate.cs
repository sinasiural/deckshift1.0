using System.Collections.Generic;
using UnityEngine;

// Opens a Gate once every enemy it watches is dead. The tutorial's card lessons use it: "kill that
// zombie with a card" only teaches casting if the way on is actually shut until you do.
//
// ⚠️ This is a TUTORIAL-ONLY exception to Level Design Law 1 (every level finishable with movement
// alone), approved by the designer for the tutorial room. Do not reuse it in run rooms.
//
// Death is detected by the reference going null: EnemyHealth.Die() destroys the enemy in the same
// frame it dies, so there is no "dead but present" state to test.
public class TutorialGate : MonoBehaviour
{
    [SerializeField] private Gate gate;
    [SerializeField] private List<EnemyHealth> watched = new List<EnemyHealth>();

    private bool opened;

    public void Configure(Gate target, List<EnemyHealth> enemies)
    {
        gate = target;
        watched = enemies ?? new List<EnemyHealth>();
    }

    private void Start()
    {
        // An empty list would open the gate on the first frame, silently skipping the lesson. Say so
        // instead, and leave the gate shut so the mistake is obvious in play.
        if (watched.Count == 0)
            Debug.LogWarning($"TutorialGate on '{name}' watches no enemies and will never open.", this);
    }

    private void Update()
    {
        if (opened || gate == null || watched.Count == 0) return;

        foreach (EnemyHealth e in watched)
            if (e != null) return;

        opened = true;
        gate.Open();
    }
}
