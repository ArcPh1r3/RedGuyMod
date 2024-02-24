using EntityStates;
using EntityStates.CaptainDefenseMatrixItem;
using RedGuyMod.Content;
using RoR2;
using RoR2.Orbs;
using RoR2.Projectile;
using RoR2.UI;
using System.Collections.Specialized;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static RoR2.CameraTargetParams;

namespace RedGuyMod.SkillStates.Ravager
{
    public class ChargeBeam : BaseRavagerSkillState
    {
        public float baseDuration = 30f;
        public static float maxDamageCoefficient = 24f;
        public static float minDamageCoefficient = 2f;
        public float minRadius = 0.1f;
        public float maxRadius = 14f;

        private float duration;
        private float stopwatch;
        private GameObject chargeEffectInstance;
        private CameraParamsOverrideHandle camParamsOverrideHandle;
        protected GameObject areaIndicatorInstance { get; set; }
        private uint playId;
        private bool isCharged;
        private bool ending;

        private CrosshairUtils.OverrideRequest crosshairOverrideRequest;
        private bool allySucc;

        public override void OnEnter()
        {
            base.OnEnter();
            if (this.penis) this.penis.projectilesDeleted = 0;
            this.duration = this.baseDuration / this.attackSpeedStat;
            this.camParamsOverrideHandle = Modules.CameraParams.OverrideCameraParams(base.cameraTargetParams, RavagerCameraParams.AIM, 5f);
            this.allySucc = Modules.Config.allySucc.Value;

            base.PlayCrossfade("Gesture, Override", "ChargeBeam", "Beam.playbackRate", this.duration * 0.15f, 0.05f);

            this.chargeEffectInstance = GameObject.Instantiate(Modules.Assets.beamChargeEffect);
            this.chargeEffectInstance.transform.parent = this.FindModelChild("HandL");
            this.chargeEffectInstance.transform.localPosition = new Vector3(-0.5f, 0f, -0.2f);
            this.chargeEffectInstance.transform.localRotation = Quaternion.identity;

            if (NetworkServer.active) this.characterBody.AddBuff(RoR2Content.Buffs.Slow50);

            if (EntityStates.Huntress.ArrowRain.areaIndicatorPrefab)
            {
                //this.areaIndicatorInstance = UnityEngine.Object.Instantiate<GameObject>(EntityStates.Huntress.ArrowRain.areaIndicatorPrefab);
                //this.areaIndicatorInstance.transform.localScale = Vector3.zero;
            }

            this.crosshairOverrideRequest = CrosshairUtils.RequestOverrideForBody(this.characterBody, Modules.Assets.beamCrosshair, CrosshairUtils.OverridePriority.Skill);

            Util.PlaySound("sfx_ravager_charge_beam", this.gameObject);
            this.playId = Util.PlaySound("sfx_ravager_beam_loop", this.gameObject);
        }

        public override void OnExit()
        {
            base.OnExit();

            if (this.charge < 0.5f) base.PlayAnimation("Gesture, Override", "FireBeam", "Beam.playbackRate", 1f);

            if (this.areaIndicatorInstance) EntityState.Destroy(this.areaIndicatorInstance.gameObject);

            this.cameraTargetParams.RemoveParamsOverride(this.camParamsOverrideHandle);
            if (this.crosshairOverrideRequest != null) this.crosshairOverrideRequest.Dispose();

            if (this.chargeEffectInstance) Destroy(this.chargeEffectInstance);

            if (NetworkServer.active)
            {
                this.characterBody.RemoveBuff(RoR2Content.Buffs.Slow50);

                int j = this.characterBody.GetBuffCount(Content.Survivors.RedGuy.projectileEatedBuff);
                for (int i = 0; i < j; i++)
                {
                    this.characterBody.RemoveBuff(Content.Survivors.RedGuy.projectileEatedBuff);
                }
            }

            AkSoundEngine.StopPlayingID(this.playId);

            this.penis.projectilesDeleted = 0;
        }

        private float charge
        {
            get
            {
                float value = Mathf.Clamp(this.stopwatch + (this.characterBody.GetBuffCount(Content.Survivors.RedGuy.projectileEatedBuff) * 1.25f), 0f, this.duration);
                return Util.Remap(value, 0f, this.duration, 0f, 1f);
            }
        }

        public override void Update()
        {
            base.Update();
            this.UpdateAreaIndicator();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            this.stopwatch += Time.fixedDeltaTime;
            if (this.empowered) this.stopwatch += 10f * Time.fixedDeltaTime;
            this.characterBody.outOfCombatStopwatch = 0f;
            this.StartAimMode(0.5f);
            this.characterBody.isSprinting = false;

            if (this.areaIndicatorInstance)
            {
                float size = Util.Remap(this.charge, 0f, 1f, this.minRadius, this.maxRadius);
                this.areaIndicatorInstance.transform.localScale = new Vector3(size, size, size);
            }

            if (!this.isCharged)
            {
                if (this.charge >= 1f)
                {
                    this.isCharged = true;
                    Util.PlaySound("sfx_ravager_beam_maxcharge", this.gameObject);
                    base.PlayCrossfade("Gesture, Override", "ChargeBeamMax", "Beam.playbackRate", 1f, 0.05f);
                }
            }

            this.CheckForProjectiles();

            this.chargeEffectInstance.transform.localScale = Vector3.one * Util.Remap(this.charge, 0f, 1f, 0f, 2f);
            this.penis.chargeValue = this.charge;

            if (base.isAuthority)
            {
                if (!this.inputBank.skill3.down)
                {
                    this.FireBeam();
                    return;
                }
            }
        }

        private void UpdateAreaIndicator()
        {
            if (this.areaIndicatorInstance)
            {
                float maxDistance = 3000f;

                Ray aimRay = base.GetAimRay();
                RaycastHit raycastHit;
                if (Physics.Raycast(aimRay, out raycastHit, maxDistance, LayerIndex.CommonMasks.bullet))
                {
                    this.areaIndicatorInstance.transform.position = raycastHit.point;
                    this.areaIndicatorInstance.transform.up = raycastHit.normal;
                }
                else
                {
                    this.areaIndicatorInstance.transform.position = aimRay.GetPoint(maxDistance);
                    this.areaIndicatorInstance.transform.up = -aimRay.direction;
                }
            }
        }

        protected virtual void EatProjectile(ProjectileController pc)
        {
            Util.PlaySound("sfx_ravager_beam_consume", pc.gameObject);

            if (NetworkServer.active)
            {
                this.characterBody.AddBuff(Content.Survivors.RedGuy.projectileEatedBuff);
                this.penis.projectilesDeleted++;

                if (base.fixedAge <= 0.5f)
                {
                    this.characterBody.AddBuff(Content.Survivors.RedGuy.projectileEatedBuff);
                    this.penis.projectilesDeleted++;

                    this.characterBody.AddBuff(Content.Survivors.RedGuy.projectileEatedBuff);
                    this.penis.projectilesDeleted++;

                    this.characterBody.AddBuff(Content.Survivors.RedGuy.projectileEatedBuff);
                    this.penis.projectilesDeleted++;
                }

                GameObject.Destroy(pc.gameObject);

                if (DefenseMatrixOn.tracerEffectPrefab)
                {
                    EffectData effectData = new EffectData
                    {
                        origin = this.chargeEffectInstance.transform.position,
                        start = pc.transform.position
                    };
                    EffectManager.SpawnEffect(DefenseMatrixOn.tracerEffectPrefab, effectData, true);
                }
            }
        }

        protected virtual void CheckForProjectiles()
        {
            Collider[] array = Physics.OverlapSphere(this.chargeEffectInstance.transform.position, 12f, LayerIndex.projectile.mask);

            for (int i = 0; i < array.Length; i++)
            {
                ProjectileController pc = array[i].GetComponentInParent<ProjectileController>();
                if (pc)
                {
                    if (pc.owner != this.gameObject)
                    {
                        if (!this.allySucc)
                        {
                            if (pc.teamFilter.teamIndex != this.GetTeam())
                            {
                                this.EatProjectile(pc);
                            }
                        }
                        else
                        {
                            this.EatProjectile(pc);
                        }
                    }
                }
            }
        }

        protected virtual void FireBeam()
        {
            float storedCharge = this.charge;

            float recoilAmplitude = Util.Remap(storedCharge, 0f, 1f, 1f, 16f);// / this.attackSpeedStat;

            base.AddRecoil(-0.4f * recoilAmplitude, -0.8f * recoilAmplitude, -0.3f * recoilAmplitude, 0.3f * recoilAmplitude);
            this.characterBody.AddSpreadBloom(4f);
            if (charge >= 0.75f) EffectManager.SimpleMuzzleFlash(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/ImpBoss/ImpBossBlink.prefab").WaitForCompletion(), gameObject, "HandL", true);
            else if (charge >= 0.25f) EffectManager.SimpleMuzzleFlash(Modules.Assets.cssEffect, gameObject, "HandL", true);
            else EffectManager.SimpleMuzzleFlash(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Golem/MuzzleflashGolem.prefab").WaitForCompletion(), gameObject, "HandL", true);
            Util.PlaySound("sfx_ravager_blast", this.gameObject);
            //tsuyoikenko big blast m1 change. Can't test/build cause csproj is git hidden? or spaghetti
            if (storedCharge >= 0.5f && this.inputBank.skill1.down)
            {
                #region BigBlast

                GameObject tracer = null;
                GameObject impact = null;

                tracer = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/Railgunner/TracerRailgunSuper.prefab").WaitForCompletion();
                tracer.transform.GetChild(4).GetChild(3).localScale = Vector3.one * 6f;
                impact = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Golem/ExplosionGolem.prefab").WaitForCompletion();

                float damage = Util.Remap(storedCharge, 0f, 1f, ChargeBeam.minDamageCoefficient, ChargeBeam.maxDamageCoefficient) * this.damageStat;

                Ray aimRay = GetAimRay();

                BulletAttack bulletAttack = new BulletAttack
                {
                    aimVector = aimRay.direction,
                    origin = aimRay.origin,
                    damage = damage,
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.Stun1s,
                    falloffModel = BulletAttack.FalloffModel.None,
                    maxDistance = 2000f,
                    force = Util.Remap(storedCharge, 0f, 1f, 5f, 5000f),
                    hitMask = LayerIndex.CommonMasks.bullet,
                    isCrit = this.RollCrit(),
                    owner = this.gameObject,
                    muzzleName = "HandL",
                    smartCollision = true,
                    procChainMask = default,
                    procCoefficient = 1f,
                    radius = Util.Remap(storedCharge, 0f, 1f, 0f, 5f),
                    sniper = false,
                    stopperMask = LayerIndex.noCollision.mask,
                    weapon = null,
                    tracerEffectPrefab = tracer,
                    spreadPitchScale = 1f,
                    spreadYawScale = 1f,
                    queryTriggerInteraction = QueryTriggerInteraction.UseGlobal,
                    hitEffectPrefab = impact,
                    HitEffectNormal = impact,
                    maxSpread = 0f,
                    minSpread = 0f,
                    bulletCount = 1
                };
                bulletAttack.Fire();

                this.outer.SetNextStateToMain();
                #endregion
            }
            else if (storedCharge >= 0.5f)
            {
                this.outer.SetNextState(new FireBeam
                {
                    charge = storedCharge
                });
                return;
            }
            else
            {
                GameObject tracer = null;
                GameObject impact = null;

                tracer = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/Railgunner/TracerRailgunSuper.prefab").WaitForCompletion();
                impact = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Golem/ExplosionGolem.prefab").WaitForCompletion();

                float damage = Util.Remap(storedCharge, 0f, 1f, ChargeBeam.minDamageCoefficient, ChargeBeam.maxDamageCoefficient) * this.damageStat;

                Ray aimRay = GetAimRay();

                BulletAttack bulletAttack = new BulletAttack
                {
                    aimVector = aimRay.direction,
                    origin = aimRay.origin,
                    damage = damage,
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.Stun1s,
                    falloffModel = BulletAttack.FalloffModel.None,
                    maxDistance = 2000f,
                    force = Util.Remap(storedCharge, 0f, 1f, 5f, 5000f),
                    hitMask = LayerIndex.CommonMasks.bullet,
                    isCrit = this.RollCrit(),
                    owner = this.gameObject,
                    muzzleName = "HandL",
                    smartCollision = true,
                    procChainMask = default,
                    procCoefficient = 1f,
                    radius = Util.Remap(storedCharge, 0f, 1f, 0f, 5f),
                    sniper = false,
                    stopperMask = LayerIndex.noCollision.mask,
                    weapon = null,
                    tracerEffectPrefab = tracer,
                    spreadPitchScale = 1f,
                    spreadYawScale = 1f,
                    queryTriggerInteraction = QueryTriggerInteraction.UseGlobal,
                    hitEffectPrefab = impact,
                    HitEffectNormal = impact,
                    maxSpread = 0f,
                    minSpread = 0f,
                    bulletCount = 1
                };
                bulletAttack.Fire();

                this.outer.SetNextStateToMain();
            }

            /*if (base.isAuthority)
            {
                float damage = Util.Remap(storedCharge, 0f, 1f, ChargeBeam.minDamageCoefficient, ChargeBeam.maxDamageCoefficient) * this.damageStat;
                float radius = Util.Remap(storedCharge, 0f, 1f, this.minRadius, this.maxRadius);

                BlastAttack blastAttack = new BlastAttack();
                blastAttack.radius = radius;
                blastAttack.procCoefficient = 1f;
                blastAttack.position = this.areaIndicatorInstance.transform.position;
                blastAttack.attacker = this.gameObject;
                blastAttack.crit = this.RollCrit();
                blastAttack.baseDamage = damage;
                blastAttack.falloffModel = BlastAttack.FalloffModel.None;
                blastAttack.baseForce = Util.Remap(storedCharge, 0f, 1f, 10f, 8000f);
                blastAttack.teamIndex = this.teamComponent.teamIndex;
                blastAttack.damageType = DamageType.Stun1s;
                blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;

                blastAttack.Fire();
            }*/

            // effect

            /*Transform modelTransform = this.GetModelTransform();
            if (modelTransform)
            {
                ChildLocator component = modelTransform.GetComponent<ChildLocator>();
                if (component)
                {
                    int childIndex = component.FindChildIndex("HandL");
                    if (EntityStates.GolemMonster.FireLaser.tracerEffectPrefab)
                    {
                        EffectData effectData = new EffectData
                        {
                            origin = this.areaIndicatorInstance.transform.position,
                            start = this.GetAimRay().origin
                        };
                        effectData.SetChildLocatorTransformReference(base.gameObject, childIndex);
                        EffectManager.SpawnEffect(EntityStates.GolemMonster.FireLaser.tracerEffectPrefab, effectData, true);
                        EffectManager.SpawnEffect(EntityStates.GolemMonster.FireLaser.hitEffectPrefab, effectData, true);
                    }
                }
            }*/
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Pain;
        }
    }
}