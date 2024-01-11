using EntityStates;
using RoR2;
using RoR2.Audio;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace RedGuyMod.SkillStates.BaseStates
{
    public class BaseMeleeAttack : SkillStates.Ravager.BaseRavagerSkillState
    {
        public int swingIndex;

        protected string hitboxName = "Sword";

        protected DamageType damageType = DamageType.Generic;
        protected float damageCoefficient = 3.5f;
        protected float procCoefficient = 1f;
        protected float pushForce = 300f;
        protected Vector3 bonusForce = Vector3.zero;
        protected float baseDuration = 1f;
        protected float attackStartTime = 0.2f;
        protected float attackEndTime = 0.4f;
        protected float baseEarlyExitTime = 0.4f;
        protected float hitStopDuration = 0.012f;
        protected float attackRecoil = 0.75f;
        protected float hitHopVelocity = 4f;
        protected bool smoothHitstop = false;

        protected string swingSoundString = "";
        protected string hitSoundString = "";
        protected string muzzleString = "SwingCenter";
        protected GameObject swingEffectPrefab;
        protected GameObject hitEffectPrefab;
        protected NetworkSoundEventIndex impactSound;

        private float earlyExitTime;
        public float duration;
        private bool hasFired;
        private float hitPauseTimer;
        protected OverlapAttack attack;
        private bool inHitPause;
        private bool hasHopped;
        protected float stopwatch;
        protected Animator animator;
        private BaseState.HitStopCachedState hitStopCachedState;
        private Vector3 storedVelocity;

        protected List<HurtBox> hitResults = new List<HurtBox>();

        public override void OnEnter()
        {
            base.OnEnter();
            this.duration = this.baseDuration / this.attackSpeedStat;
            this.earlyExitTime = this.baseEarlyExitTime;// / this.attackSpeedStat;
            this.hasFired = false;
            this.animator = base.GetModelAnimator();
            base.StartAimMode(0.5f + this.duration, false);
            base.characterBody.outOfCombatStopwatch = 0f;
            this.animator.SetBool("attacking", true);

            HitBoxGroup hitBoxGroup = null;
            Transform modelTransform = base.GetModelTransform();

            if (modelTransform)
            {
                hitBoxGroup = Array.Find<HitBoxGroup>(modelTransform.GetComponents<HitBoxGroup>(), (HitBoxGroup element) => element.groupName == this.hitboxName);
            }

            this.PlayAttackAnimation();

            this.attack = new OverlapAttack();
            this.attack.damageType = this.damageType;
            this.attack.attacker = base.gameObject;
            this.attack.inflictor = base.gameObject;
            this.attack.teamIndex = base.GetTeam();
            this.attack.damage = this.damageCoefficient * this.damageStat;
            this.attack.procCoefficient = this.procCoefficient;
            this.attack.hitEffectPrefab = this.hitEffectPrefab;
            this.attack.forceVector = this.bonusForce;
            this.attack.pushAwayForce = this.pushForce;
            this.attack.hitBoxGroup = hitBoxGroup;
            this.attack.isCrit = base.RollCrit();
            this.attack.impactSound = this.impactSound;

            //this.characterBody.isSprinting = false;
        }

        protected virtual void FireShuriken()
        {
            PrimarySkillShurikenBehavior shurikenComponent = this.GetComponent<PrimarySkillShurikenBehavior>();
            if (shurikenComponent) shurikenComponent.OnSkillActivated(this.skillLocator.primary);
        }

        protected virtual void PlayAttackAnimation()
        {
            base.PlayCrossfade("Gesture, Override", "Slash" + (1 + swingIndex), "Slash.playbackRate", this.duration, 0.05f);
        }

        public override void OnExit()
        {
            if (!this.hasFired) this.FireAttack();

            if (this.inHitPause)
            {
                base.ConsumeHitStopCachedState(this.hitStopCachedState, base.characterMotor, this.animator);
                this.inHitPause = false;
                base.characterMotor.velocity = this.storedVelocity;
                if (this.animator) this.animator.SetFloat("Slash.playbackRate", this.duration);
            }

            base.OnExit();
        }

        protected virtual void PlaySwingEffect()
        {
            EffectManager.SimpleMuzzleFlash(this.swingEffectPrefab, base.gameObject, this.muzzleString, false);
        }

        protected virtual void OnHitEnemyAuthority(int amount)
        {
            Util.PlaySound(this.hitSoundString, base.gameObject);

            if (!this.hasHopped)
            {
                if (base.characterMotor && !base.characterMotor.isGrounded)
                {
                    base.SmallHop(base.characterMotor, this.hitHopVelocity);
                }

                this.hasHopped = true;
            }

            if (!this.inHitPause)
            {
                this.storedVelocity = base.characterMotor.velocity;
                this.hitStopCachedState = base.CreateHitStopCachedState(base.characterMotor, this.animator, "Slash.playbackRate");
                this.hitPauseTimer = this.hitStopDuration / this.attackSpeedStat;
                this.inHitPause = true;
            }
        }

        protected virtual void FireAttack()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;
                Util.PlayAttackSpeedSound(this.swingSoundString, base.gameObject, this.attackSpeedStat);

                this.PlaySwingEffect();

                if (base.isAuthority)
                {
                    base.AddRecoil(-1f * this.attackRecoil, -2f * this.attackRecoil, -0.5f * this.attackRecoil, 0.5f * this.attackRecoil);
                }
            }

            if (base.isAuthority)
            {
                this.hitResults.Clear();
                if (this.attack.Fire(this.hitResults))
                {
                    this.OnHitEnemyAuthority(this.hitResults.Count);
                }
            }
        }

        protected virtual void SetNextState()
        {
            int index = this.swingIndex;
            if (index == 0) index = 1;
            else index = 0;

            this.outer.SetNextState(new BaseMeleeAttack
            {
                swingIndex = index
            });
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            this.hitPauseTimer -= Time.fixedDeltaTime;

            if (this.hitPauseTimer <= 0f && this.inHitPause)
            {
                base.ConsumeHitStopCachedState(this.hitStopCachedState, base.characterMotor, this.animator);
                this.inHitPause = false;
                base.characterMotor.velocity = this.storedVelocity;
            }

            if (!this.inHitPause)
            {
                this.stopwatch += Time.fixedDeltaTime;
            }
            else
            {
                if (base.characterMotor) base.characterMotor.velocity = Vector3.zero;
                if (this.animator) this.animator.SetFloat("Slash.playbackRate", 0f);
            }

            if (this.stopwatch >= (this.duration * this.attackStartTime) && this.stopwatch <= (this.duration * this.attackEndTime))
            {
                this.FireAttack();
            }

            if (base.fixedAge >= (this.duration * this.earlyExitTime) && base.isAuthority)
            {
                if (base.inputBank.skill1.down)
                {
                    if (!this.hasFired) this.FireAttack();
                    this.SetNextState();
                    return;
                }
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(this.swingIndex);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            this.swingIndex = reader.ReadInt32();
        }
    }
}