# Gameplay invariants

The brain is a pure, deterministic function of `(WorldSnapshot, CommanderState)`. These invariants keep the
campaign reproducible and resumable; `Nucleus.Sim.Tests` is the canary.

- **Determinism.** Same seed ⇒ identical trace; save then resume ⇒ byte-identical continuation. The canary
  tests assert *internal* determinism and save/resume round-trip (not equality to a frozen golden), so behavior
  may evolve — but it must stay deterministic and round-trip. See [determinism](determinism.md).
- **Default reproduces baseline.** A `CommanderState` with no genome (default `Doctrine`) and the all-0.5
  default `CommanderGenome` must produce the stock behavior. Every personality gene is centered so 0.5 = no
  change; `GenomeTests` pins both the neutral default and the gene→knob direction.
- **The war must progress.** New objective kinds or scoring must not stall the war or starve the offence (the
  home-defence proliferation trap). A change that leaves enemies un-attrited is a regression, not a tuning knob.
- **Personality via genome, not forks.** `GenomeFactory` derives a deterministic genome per `(campaignSeed,
  faction)`; `Doctrine.ApplyGenome` maps genes to knobs the brain reads. The same brain drives every commander.
- **No phantom objectives.** Only auto-generate an objective kind whose effect the brain actually models
  (Recon, Capture, Destroy, DefendArea, ControlAirspace). Resupply stays player-only — there is no resupply
  effect to drive, so an AI Resupply objective would be a no-op.
- **Save format.** Persisted fields change together with their readers/writers; `CampaignSave` is forward-compat
  (unknown records ignored, missing columns degrade gracefully). `CampaignStoreTests` guards the round-trip.
