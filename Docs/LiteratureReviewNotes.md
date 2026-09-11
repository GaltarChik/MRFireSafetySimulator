# Literature Review — Working Notes

**Scope:** mixed-reality training systems and computational models of fire spread, as background for
the MR Fire Safety Training Simulator.

> **How to use this file.** These are structured working notes, not finished prose. The anchor
> references below are established works in their fields, but **every bibliographic detail must be
> verified against the publisher record before it enters the thesis** — page ranges and volume
> numbers in particular. Sections marked **[GAP]** require a database search that only you can run
> against your university's subscriptions; do not cite anything you have not read.

---

## 1. Research questions

1. What does mixed reality offer for fire-safety training that fully immersive VR does not?
2. Which computational fire-spread models are tractable inside a mobile XR frame budget?
3. How is training performance measured objectively in XR safety training?

---

## 2. Theme A — The reality–virtuality continuum

**Anchor references**

- Milgram, P., & Kishino, F. (1994). *A Taxonomy of Mixed Reality Visual Displays.* IEICE
  Transactions on Information Systems, E77-D(12).
- Azuma, R. T. (1997). *A Survey of Augmented Reality.* Presence: Teleoperators and Virtual
  Environments, 6(4).

**Why it matters here.** Milgram and Kishino's continuum is the standard framing for positioning this
work: the system is not at the virtual end (the user is not transported elsewhere) but in the mixed
region, where virtual content is registered with the real environment. Azuma's three criteria for
augmented reality — combines real and virtual, interactive in real time, registered in 3D — map
directly onto the requirements of this prototype and can be used as an evaluation checklist.

**Argument to develop.** Passthrough MR preserves the trainee's proprioception and spatial awareness
of the real room. In fire-extinguisher training this is not a convenience but the point: the trainee
must keep an escape route in view, and an instructor must be able to stand beside them safely.

---

## 3. Theme B — Immersion, presence and training transfer

**Anchor references**

- Bowman, D. A., & McMahan, R. P. (2007). *Virtual Reality: How Much Immersion Is Enough?* Computer,
  40(7).
- Slater, M., & Sanchez-Vives, M. V. (2016). *Enhancing Our Lives with Immersive Virtual Reality.*
  Frontiers in Robotics and AI, 3.

**Argument to develop.** Higher immersion is not monotonically better; what matters is whether the
simulated task exercises the same perceptual and motor skills as the real one. Extinguisher technique
— distance, sweep, aim at the base of the fire — is a spatial-motor skill, which supports the choice
of room-scale MR with a tracked hand-held controller over a seated or desktop simulation.

**[GAP] Required search — VR/AR fire-safety training studies.**
Search Scopus / Web of Science / IEEE Xplore / PubMed with:

```
("virtual reality" OR "augmented reality" OR "mixed reality")
AND ("fire safety" OR "fire extinguisher" OR "firefighter" OR "fire drill")
AND (training OR education OR "skill transfer" OR evaluation)
```

For each usable hit, record: device class, fire model used, interaction method, sample size, outcome
measures, and whether transfer to real performance was tested. Two or three empirical studies with
measured outcomes are worth more than a dozen descriptive system papers.

---

## 4. Theme C — Models of fire spread

### 4.1 Computational fluid dynamics

- McGrattan, K., et al. *Fire Dynamics Simulator — Technical Reference Guide.* NIST Special
  Publication 1018. National Institute of Standards and Technology.

FDS solves a large-eddy-simulation form of the Navier–Stokes equations for low-speed, thermally
driven flow. It is the reference standard for fire reconstruction and engineering analysis, and it is
categorically unsuited to real-time interactive use: simulation times are orders of magnitude longer
than real time. **Cite it to establish the upper bound of physical fidelity and to justify rejecting
it**, not as a candidate.

### 4.2 Empirical spread models

- Rothermel, R. C. (1972). *A Mathematical Model for Predicting Fire Spread in Wildland Fuels.* USDA
  Forest Service Research Paper INT-115.

Rothermel's model gives rate of spread as a function of fuel, moisture, wind and slope. It is
semi-empirical and cheap, but formulated for wildland fuel beds rather than for an enclosed compartment
with a discrete object burning.

### 4.3 Cellular automata

- Karafyllidis, I., & Thanailakis, A. (1997). *A model for predicting forest fire spreading using
  cellular automata.* Ecological Modelling, 99(1).
- Alexandridis, A., Vakalis, D., Siettos, C. I., & Bafas, G. V. (2008). *A cellular automata model
  for forest fire spread prediction.* Applied Mathematics and Computation, 204(1).

**This is the model family the project adopts.** A CA discretises the burning surface into cells with
a scalar state updated by a local transition rule over a neighbourhood. Cost is linear in cell count
and independent of frame rate when run on a fixed tick. The literature is dominated by wildfire
applications; the contribution here is the adaptation of the same formalism to a small,
object-scale surface with an interactive suppression term.

**Argument to develop — the selection criterion.** For extinguisher training the model does not need
to predict temperature fields correctly. It needs to reproduce the behaviours the trainee must
respond to: growth from a seed, spread across a surface, resistance to partial suppression, and
reignition from surviving cells. A CA reproduces all four; FDS would reproduce them at an
unattainable cost. State this criterion explicitly in the thesis — it is the methodological core of
the modelling chapter.

**[GAP] Required search — CA models with an extinguishing term.**
Most published CA fire models simulate spread only. Search for work that adds suppression:

```
("cellular automata" OR "cellular automaton") AND fire AND (suppression OR extinguish* OR firefighting)
```

If little exists for object-scale interactive suppression, say so plainly — an identified gap is a
legitimate and valuable finding for the thesis.

---

## 5. Theme D — Objective performance measurement

**[GAP] Required search.** Look for metric sets used in XR safety-training evaluation:

```
(virtual OR augmented OR mixed) AND reality AND training AND (metrics OR "performance measurement"
OR assessment) AND (safety OR emergency)
```

Metrics implemented in this prototype, to be positioned against whatever the literature uses:

| Metric | Rationale |
|---|---|
| Time to suppression | Primary efficiency measure |
| Agent consumed | Technique economy; discriminates sweeping from spraying |
| Object integrity remaining | Outcome quality — damage accrued before suppression |
| Final fire intensity | Completeness of suppression |
| Mean / minimum FPS | Validity control: a dropped-frame session is not comparable |

The last row is a methodological point worth making explicitly: in XR studies, frame-rate stability
is a confound for task performance, so it belongs in the recorded data rather than in a footnote.

---

## 6. Synthesis — the gap this project addresses

1. Fire-safety XR work concentrates on fully immersive VR, which discards the real training space.
2. Real-time fire models in training applications are frequently scripted animations rather than
   simulations with state, so the fire does not respond meaningfully to the trainee's technique.
3. Objective, automatically recorded performance metrics are reported inconsistently.

This prototype addresses all three: passthrough MR on the trainee's own floor, a cellular-automata
fire with an interactive suppression term, and automatic per-session metric logging.

**Caveat to state honestly in the thesis:** the prototype has not been evaluated with human
participants. Claims are therefore about system design and feasibility, not about training
effectiveness. Do not overclaim transfer.

---

## 7. Next actions

- [ ] Run the three **[GAP]** searches; record hits in a reference manager.
- [ ] Verify every bibliographic detail above against the publisher record.
- [ ] Read the CA papers closely enough to compare their transition rules with the one implemented
      in `FirePropagationSystem` (see the Technical Architecture Document, §3.2).
- [ ] Decide the citation style required by the faculty before drafting.
