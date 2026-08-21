using System.Collections.Generic;
using UnityEngine;

public class RelicManager : MonoBehaviour
{
    public static RelicManager instance;

    // --- Slot-constrained loadout (see RelicRedesign.md) ---
    // The player owns at most MaxSlots relics; list index == slot index.
    public const int BaseSlots = 5;

    // ⚠️ WAS A const, NOW A PROPERTY — Estate Sale can earn a sixth slot. Kept STATIC and under the
    // same name so all five existing `RelicManager.MaxSlots` call sites (the HUD, the manage panel,
    // the swap screen, the pause readout) pick the change up untouched. A const would have been
    // inlined into each of them at compile time and none would ever have grown.
    public static int MaxSlots => BaseSlots + (instance != null ? instance.bonusSlots : 0);

    public bool IsFull => ownedRelics.Count >= MaxSlots;

    // --- Estate Sale ---------------------------------------------------------
    // Sell five relics in one run and keep a sixth slot for the rest of it.
    public const int EstateSaleTarget = 5;
    [System.NonSerialized] public int relicsSoldThisRun = 0;
    private int bonusSlots = 0;

    /// <summary>Progress toward Estate Sale's sixth slot, for its live readout.</summary>
    public int EstateSaleProgress => Mathf.Min(relicsSoldThisRun, EstateSaleTarget);
    public bool EstateSaleClaimed => bonusSlots > 0;

    // Oyuncunun şu anda sahip olduğu tüm pasif eşyaların (Relic) listesi
    private List<RelicData> ownedRelics = new List<RelicData>();

    // Fired each time a new relic is successfully added; RelicHUD subscribes to this.
    public event System.Action<RelicData> OnRelicAdded;
    // Fired when a relic is removed (sold). HUD/panels rebuild on this too.
    public event System.Action<RelicData> OnRelicRemoved;

    // Read-only view of the owned list for HUD population on Start.
    public IReadOnlyList<RelicData> OwnedRelics => ownedRelics;

    // Fixed sell refund by rarity (RelicRedesign.md v1). Tunable.
    public int SellValueFor(RelicData relic)
    {
        if (relic == null) return 0;
        // Pawnbroker doubles it. Applied HERE rather than in SellRelic so every surface that quotes
        // a price — the tooltip, the manage panel, the swap screen, a declined chest's payout —
        // quotes the one the player will actually be paid.
        //
        // It doubles its OWN sale too, since the value is read while it is still worn. That is the
        // honest reading and it is a fine last move: cash out the pawnbroker last.
        int mult = HasRelic("Pawnbroker") ? 2 : 1;
        return BaseSellValue(relic) * mult;
    }

    private int BaseSellValue(RelicData relic)
    {
        switch (relic.rarity)
        {
            // A boss relic is still sellable, or a full loadout would make one unclaimable — the
            // swap screen needs something to offer. Priced above Legendary because it cost a boss.
            case Rarity.Boss:      return 200;
            case Rarity.Legendary: return 150;
            case Rarity.Epic:      return 90;
            case Rarity.Rare:      return 50;
            default:               return 25; // Common
        }
    }

    // Sells a relic: removes it from the loadout, credits its gold value, and fires
    // OnRelicRemoved so the HUD/panels reflow. No-op if the relic isn't owned.
    public void SellRelic(RelicData relic)
    {
        if (relic == null || !ownedRelics.Contains(relic)) return;

        int value = SellValueFor(relic);
        ownedRelics.Remove(relic);

        if (GameManager.instance != null && GameManager.instance.player != null)
            GameManager.instance.player.AddGold(value);

        // Estate Sale: five sales in a run buys a sixth slot.
        //
        // ⚠️ COUNTED ON EVERY SALE, not only while Estate Sale is worn — otherwise picking it up
        // late would start you at zero and the contract would be unwinnable in practice. The relic
        // is what CLAIMS the slot; the count is just the run's history.
        //
        // ⚠️ And the slot, once earned, is kept even if Estate Sale is sold. The description says
        // "permanently", and a sixth slot that vanished would strand the relic sitting in it.
        relicsSoldThisRun++;
        if (bonusSlots == 0 && relicsSoldThisRun >= EstateSaleTarget && HasRelic("EstateSale"))
        {
            bonusSlots = 1;
            Debug.Log($"🗝️ Estate Sale: {relicsSoldThisRun} relics sold — a sixth slot is yours for the run.");
        }

        Debug.Log($"Relic sold: {relic.relicName} (+{value} gold)");
        OnRelicRemoved?.Invoke(relic);

        // Foundry Rights: while owned, each relic you sell permanently raises max Shift this run.
        // Checked AFTER removal, so selling Foundry Rights itself doesn't trigger on its own sale.
        if (HasRelic("FoundryRights") && GameManager.instance != null && GameManager.instance.player != null)
            GameManager.instance.player.IncreaseMaxShift(1);

        RecomputePassives();   // reverse any stat relic that was just sold
    }

    // Per-room relic effects — called by LevelManager.SpawnNextRoom at the start of each room.
    public void OnRoomStart()
    {
        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;

        // Pocket Battery: +1 Shift at the start of each room.
        if (player != null && HasRelic("PocketBattery"))
            player.AddShift(1);

        // Flux Regulator: the first card played this room is free.
        if (HasRelic("FluxRegulator") && DeckManager.instance != null)
            DeckManager.instance.isNextCardFree = true;

        // Second Wind: sustain that can't be farmed. Per-kill healing rewards clearing rooms you
        // could have walked past; this pays the same whether you fight or not.
        if (player != null && HasRelic("SecondWind")) player.Heal(8);
    }

    // --- Stat passives -------------------------------------------------------
    // Recomputed from the player's BASE stats every time the loadout changes, so selling a relic
    // reverses it exactly. Never add/subtract incrementally — that breaks the moment relics stack
    // (e.g. Reinforced Plating + Glass Heart) or are sold out of order.
    public void RecomputePassives()
    {
        PlayerController player = GameManager.instance != null ? GameManager.instance.player : null;
        if (player == null) return;

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health == null) return;

        float flat = 0f;
        if (HasRelic("ReinforcedPlating")) flat += 15f;
        if (HasRelic("MatchedSet")) flat += 8f * MatchedPairs();

        float mult = 1f;
        if (HasRelic("GlassHeart")) mult *= 0.5f;

        health.SetMaxHealth((health.BaseMaxHealth + flat) * mult);
    }

    // --- Outgoing player damage ----------------------------------------------
    // Central modifier for damage the PLAYER deals. Every player damage source routes through
    // this. Flat bonuses apply first, then multipliers. 'target' may be null (breakable walls).
    public float ModifyPlayerDamage(float baseDamage, EnemyHealth target = null)
    {
        float dmg = baseDamage;

        // Whetstone: your first hit on each enemy deals +5. (Flag is only set while the relic is
        // owned, so acquiring it later still grants the bonus on already-wounded enemies.)
        if (target != null && !target.playerHasStruck && HasRelic("Whetstone"))
        {
            target.playerHasStruck = true;
            dmg += 5f;
        }

        // Midas Recoil: +1 damage per 25 gold carried.
        if (HasRelic("MidasRecoil") && GameManager.instance != null && GameManager.instance.player != null)
            dmg += GameManager.instance.player.currentGold / 25;

        // Sharp Practice: a flat bonus on every hit. Deliberately unlike Whetstone, which pays once
        // per enemy — this one scales with how OFTEN you hit rather than how many enemies exist.
        if (HasRelic("SharpPractice")) dmg += 2f;

        // Running on Fumes: +1 per 2 Shift you are MISSING, so the emptier you are the harder you
        // hit. Reads from max, which the player can raise (Nest Egg, quests), so the ceiling grows.
        if (HasRelic("RunningOnFumes") && GameManager.instance != null && GameManager.instance.player != null)
        {
            PlayerController p = GameManager.instance.player;
            dmg += Mathf.Max(0, p.maxShift - p.GetCurrentShift()) / 2;
        }

        // Matched Set: +2 per pair of relics sharing a rarity.
        if (HasRelic("MatchedSet")) dmg += 2f * MatchedPairs();

        // --- multipliers below this line ---

        // Glass Heart: double damage (paid for with half max HP).
        if (HasRelic("GlassHeart")) dmg *= 2f;

        // Odd Socket: every slot you DON'T fill makes what you do carry hit harder.
        if (HasRelic("OddSocket")) dmg *= 1f + 0.15f * EmptySlots();

        // Weight Class: heavier, so it lands harder. The jump/fall half lives on PlayerController.
        if (HasRelic("WeightClass")) dmg *= 1.4f;

        // Blompo's damage-time blessings. They live at this chokepoint rather than at the seven
        // damage call sites for the same reason the relics do — a damage source added later cannot
        // forget to honour them. It is also the only place the TARGET is known, which is what lets
        // Finisher and Opener read the enemy's health instead of guessing at cast time.
        if (DeckManager.instance != null)
            dmg = CardEnhancements.ModifyDamage(DeckManager.instance.AttributedCard, dmg, target);

        return dmg;
    }

    // --- Incoming player damage ----------------------------------------------
    // The mirror of ModifyPlayerDamage, and it exists for the same reason: one chokepoint, so a
    // damage source added later cannot forget to honour a relic.
    //
    // ⚠️ CALLED FROM TakeDamage, NOT ApplyDamage. PayHealthCost routes through ApplyDamage too, and
    // that is Stagger's bill — a price the player CHOSE to pay, not a hit taken. Scaling there
    // would make Paper Skin quietly raise Stagger's cost by 50%, which its text does not say.
    public float ModifyIncomingDamage(float damage)
    {
        float dmg = damage;

        // Paper Skin: charges bought with fragility.
        if (HasRelic("PaperSkin")) dmg *= 1.5f;

        // Odd Socket: an empty slot protects as well as it strikes.
        if (HasRelic("OddSocket")) dmg *= Mathf.Max(0f, 1f - 0.15f * EmptySlots());

        return dmg;
    }

    /// <summary>Slots left unfilled. Odd Socket reads this, so it changes the moment you sell.</summary>
    public int EmptySlots()
    {
        return Mathf.Max(0, MaxSlots - ownedRelics.Count);
    }

    /// <summary>
    /// How many PAIRS of owned relics share a rarity — three Commons is one pair, four is two.
    /// Counts Matched Set itself, which is intended: it needs a partner to do anything at all.
    /// </summary>
    public int MatchedPairs()
    {
        var byRarity = new Dictionary<Rarity, int>();
        foreach (RelicData r in ownedRelics)
        {
            if (r == null) continue;
            byRarity[r.rarity] = (byRarity.ContainsKey(r.rarity) ? byRarity[r.rarity] : 0) + 1;
        }
        int pairs = 0;
        foreach (var kv in byRarity) pairs += kv.Value / 2;
        return pairs;
    }

    // --- Phoenix Cog ---------------------------------------------------------
    [Header("Phoenix Cog")]
    [Tooltip("Optional screech/eruption clip played when Phoenix Cog saves you.")]
    public AudioClip phoenixSound;
    [Range(0f, 2f)] public float phoenixVolume = 1.3f;

    private bool phoenixUsed = false;

    // Returns true at most once per run, and only while the relic is owned.
    public bool TryConsumePhoenixCog()
    {
        if (phoenixUsed || !HasRelic("PhoenixCog")) return false;
        phoenixUsed = true;
        return true;
    }

    // --- Ace Up the Sleeve ---------------------------------------------------
    // Phoenix Cog for the OTHER death clock: the game has more than one way to lose and only
    // running out of health had a miracle. Same once-per-run shape, same consume-on-use pattern.
    private bool aceUsed = false;
    public bool AceUpTheSleeveReady => !aceUsed && HasRelic("AceUpTheSleeve");

    public bool TryConsumeAceUpTheSleeve()
    {
        if (aceUsed || !HasRelic("AceUpTheSleeve")) return false;
        aceUsed = true;
        return true;
    }

    // Screen-clearing eruption that fires when Phoenix Cog saves you. The freeze-frame, shake,
    // slow-mo and the whole rebirth spectacle live in PhoenixRebirthVFX (its own GameObject, so it
    // outlives the hit-stop); this just deals the damage and lights the fuse.
    public void PhoenixBlast(Vector3 origin)
    {
        EnemyHealth[] all = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        foreach (EnemyHealth enemy in all)
            if (enemy != null) enemy.TakeDamage(60f);

        GameObject fx = new GameObject("PhoenixRebirthVFX");
        fx.transform.position = origin + Vector3.up * 0.9f;
        Transform follow = GameManager.instance != null && GameManager.instance.player != null
            ? GameManager.instance.player.transform
            : null;
        fx.AddComponent<PhoenixRebirthVFX>().Play(follow, phoenixSound, phoenixVolume);

        Debug.Log("🔥 Phoenix Cog: survived a lethal hit and erupted!");
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            // DontDestroyOnLoad(gameObject); // (Gelecekte sahneler arası geçiş olursa bu gerekebilir)
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Oyuncuya yeni bir pasif eşya ekler. (Slot Machine bu fonksiyonu çağıracak)
    /// </summary>
    public void AddRelic(RelicData newRelic)
    {
        if (ownedRelics.Contains(newRelic))
        {
            Debug.LogWarning($"Oyuncu zaten '{newRelic.relicName}' eşyasına sahip.");
            return;
        }

        ownedRelics.Add(newRelic);
        Debug.Log($"Yeni eşya kazanıldı: {newRelic.relicName}");

        OnRelicAdded?.Invoke(newRelic);
        RecomputePassives();   // apply stat relics (Reinforced Plating, Glass Heart, ...)
    }

    // Central grant entry point (RelicRedesign.md Stage 3). All grant sources route through this.
    // Slot free  -> add immediately and run onAcquired.
    // Slots full -> open the Swap Screen; TAKE sells the chosen relic then adds this one and runs
    //               onAcquired, LEAVE runs nothing. onAcquired is where the caller finalizes side
    //               effects (e.g. the shop charges gold only when the relic is actually taken), so
    //               a declined full-slot grant costs nothing.
    // onDeclined fires only when the player refuses a full-slot swap. Sources that must always pay
    // out something (chests) use it to hand over a consolation; sources where declining should cost
    // nothing and give nothing (the shop) leave it null.
    public void TryGrantRelic(RelicData relic, System.Action onAcquired = null, System.Action onDeclined = null)
    {
        if (relic == null || ownedRelics.Contains(relic))
        {
            // Nothing to grant. Treat it as a decline so the caller can still pay out — otherwise
            // the reward evaporates silently.
            onDeclined?.Invoke();
            return;
        }

        if (!IsFull)
        {
            AddRelic(relic);
            onAcquired?.Invoke();
        }
        else
        {
            RelicSwapScreen.Open(relic, onAcquired, onDeclined);
        }
    }

    /// <summary>
    /// Diğer script'lerin (PlayerController gibi) oyuncuda bir eşya olup olmadığını
    /// ID'sine (kimliğine) bakarak kontrol etmesini sağlar.
    /// </summary>
    public bool HasRelic(string relicID)
    {
        foreach (RelicData relic in ownedRelics)
        {
            if (relic.relicID == relicID)
            {
                return true; // Eşya bulundu
            }
        }
        return false; // Eşya bulunamadı
    }

    public void OnEnemyKilled()
    {
        // RELIC 1: Vampire Tooth (ID: VampireTooth)
        // Özellik: Düşman öldürünce +5 Can
        if (HasRelic("VampireTooth"))
        {
            if (GameManager.instance != null && GameManager.instance.player != null)
            {
                GameManager.instance.player.Heal(5);
                Debug.Log("🧛 Vampire Tooth: +5 Can kazanıldı!");
            }
        }

        // RELIC 2: Kinetic Capacitor (ID: KineticCapacitor)
        // Özellik: Düşman öldürünce +2 Shift
        if (HasRelic("KineticCapacitor"))
        {
            if (GameManager.instance != null && GameManager.instance.player != null)
            {
                GameManager.instance.player.AddShift(2);
                Debug.Log("⚡ Kinetic Capacitor: +2 Shift kazanıldı!");
            }
        }

        // Quick Hands: a kill draws a card. This is the ONLY source of cards outside Recall, which
        // is the point — it makes fighting the way you refill your hand instead of paying Shift for
        // it. DrawCard is a no-op on a full hand and reshuffles the discard when the draw pile runs
        // out, so it needs no guard of its own.
        if (HasRelic("QuickHands") && DeckManager.instance != null)
            DeckManager.instance.DrawCard();
    }

    // 2. Oyuncu hasar aldığında bu fonksiyon çağrılacak
    public void OnPlayerTakeDamage()
    {
        // RELIC 3: Spiked Carapace (ID: SpikedCarapace)
        // Özellik: Hasar alınca etraftaki düşmanlara hasar yansıt
        if (HasRelic("SpikedCarapace"))
        {
            if (GameManager.instance != null && GameManager.instance.player != null)
            {
                PlayerController player = GameManager.instance.player;

                // Oyuncunun 3 birim etrafındaki düşmanları bul
                Collider2D[] enemies = Physics2D.OverlapCircleAll(player.transform.position, 3f, player.enemyLayer);

                foreach (Collider2D enemy in enemies)
                {
                    // Düşmanın can scriptine eriş (Script adın EnemyHealth ise)
                    IDamageable target = enemy.GetComponentInParent<IDamageable>();
                    if (target != null)
                    {
                        target.TakeDamage(20f); // 20 Hasar yansıt
                    }
                }
                Debug.Log("🌵 Spiked Carapace: Düşmanlara hasar yansıtıldı!");
            }
        }
    }
}