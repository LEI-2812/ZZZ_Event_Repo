using System;
using System.Linq;
using System.Collections.Generic;

namespace BeastBeat
{
    public class Fighter
    {
        public int id, level, maxHp, hp, atk, def, stateTurns;
        public int[] pp;
        public Condition state = Condition.Ready;
        public bool guard;
        public bool Alive { get { return hp > 0; } }
    }
    public class BattleMessage
    {
        public string text; public bool enemy;
        public string skillText; // 사용 시점의 이름을 보관하여 이후 교체와 무관하게 표시합니다.
        public BattleMessage(string t, bool e = false, string usedSkill = null) { text = t; enemy = e; skillText = usedSkill; }
    }
    public class BattleEngine
    {
        public readonly ProgressService Progress;
        public readonly StageData Stage;
        public readonly Fighter[] player, enemy;
        public int playerIndex, enemyIndex, round = 1;
        public bool Finished, Won;
        public BattleOutcome Outcome;
        readonly Random random;
        public Fighter Player { get { return player[playerIndex]; } }
        public Fighter Enemy { get { return enemy[enemyIndex]; } }
        public bool NeedsSwitch { get { return !Finished && !Player.Alive; } }
        public BattleEngine(ProgressService p, int stage, int seed = -1, PartyEntry[] initialParty = null)
        {
            Progress = p; Stage = p.Data.Stage(stage); random = seed < 0 ? new Random() : new Random(seed);
            if (!p.Eligible || !p.StageOpen(stage) || (initialParty == null ? p.Save.party.Count == 0 : initialParty.Length == 0)) throw new InvalidOperationException("Battle not eligible");
            player = initialParty == null ? p.Save.party.Select(id => Create(id, p.Save.level)).ToArray() : initialParty.Select(row => {
                var fighter = Create(row.bid, p.Save.level);
                // 최대 HP는 레벨 공식이 기준입니다. party에는 기존 체력 비율만 남아 있어도 됩니다.
                if (row.max_hp > 0 && row.hp > row.max_hp) throw new InvalidOperationException("party hp exceeds max_hp");
                fighter.hp = row.state == 5 ? 0 : row.max_hp > 0 ?
                    (int)Math.Floor(fighter.maxHp * (row.hp / (double)row.max_hp)) : Math.Min(fighter.maxHp, row.hp);
                fighter.state = row.state == 5 ? Condition.Ready : (Condition)row.state;
                if (fighter.state == Condition.Paralysis && p.Data.Boo(row.bid).type == (int)Element.Electric) fighter.state = Condition.Ready;
                fighter.stateTurns = fighter.state == Condition.Ready ? 0 : fighter.state == Condition.Frozen ? random.Next(1,3) : 2;
                return fighter;
            }).ToArray();
            if (initialParty != null) playerIndex = Array.FindIndex(initialParty, r=>r.is_on_field==1);
            if (!Player.Alive) playerIndex = Array.FindIndex(player, f=>f.Alive);
            enemy = Stage.enemies.Select((id, i) => Create(id, Stage.enemyLevels != null && i < Stage.enemyLevels.Length ? Stage.enemyLevels[i] : Stage.level)).ToArray();
        }
        Fighter Create(int id, int level)
        {
            var b = Progress.Data.Boo(id);
            int hp = Progress.Stat(b,level,"hp");
            return new Fighter { id=id, level=level, maxHp=hp, hp=hp, atk=Progress.Stat(b,level,"atk"), def=Progress.Stat(b,level,"def"), pp=b.skillIds.Select(x => Progress.Data.Skill(x).charge_count).ToArray() };
        }
        // 진행 중인 전투의 HP·상태·편성은 유지하고, 기술 수치만 다음 행동부터 갱신합니다.
        public void ReloadSkills(GameData latest)
        {
            foreach(var f in player.Concat(enemy)) {
                var before=Progress.Data.Boo(f.id);var after=latest.Boo(f.id);
                if(!before.skillIds.SequenceEqual(after.skillIds))
                    throw new InvalidOperationException("스킬 편성이 변경되었습니다. 배틀을 다시 시작하세요.");
            }
            var replacements=Progress.Data.skills.Select(s=>latest.Skill(s.id)).ToArray();
            foreach(var f in player.Concat(enemy)) {
                var ids=Progress.Data.Boo(f.id).skillIds;
                for(int i=0;i<ids.Length;i++) {
                    int used=Math.Max(0,Progress.Data.Skill(ids[i]).charge_count-f.pp[i]);
                    f.pp[i]=Math.Max(0,latest.Skill(ids[i]).charge_count-used);
                }
            }
            Progress.Data.skills=replacements;
        }
        string Name(Fighter f) { return Progress.Data.Boo(f.id).name; }
        // 동일한 계산을 아군 공격/적군 공격/AI 스킬 선택에 사용합니다.
        int ApplyDamage(double baseDamage, Fighter defender, float multiplier)
        {
            // 방어력은 아래 기본 피해 공식에 이미 반영되므로 추가로 나누지 않습니다.
            return Math.Max(1, (int)Math.Round(baseDamage * multiplier * (defender.guard ? .45 : 1)));
        }
        static double BaseDamage(Fighter attacker, Fighter defender, double skillPower)
        {
            if (defender.def <= 0) throw new InvalidOperationException("대상의 방어력은 0보다 커야 합니다.");
            return Math.Floor(((attacker.level + 5) / 10.0) * (attacker.atk / (double)defender.def) * skillPower) + 2;
        }
        public int Damage(Fighter a, Fighter b, float power, bool elemental)
        {
            float multiplier = elemental ? Elements.Multiplier(Progress.Data.Boo(a.id).type, Progress.Data.Boo(b.id).type, Progress.Data.balance) : 1;
            return ApplyDamage(BaseDamage(a,b,power), b, multiplier);
        }
        float SkillMultiplier(Fighter a, Fighter b, SkillData skill)
        {
            int type = skill.type > 0 ? skill.type : Progress.Data.Boo(a.id).type;
            return skill.effect == "element" ? Elements.Multiplier(type, Progress.Data.Boo(b.id).type, Progress.Data.balance) : 1;
        }
        public int Damage(Fighter a, Fighter b, SkillData skill)
        {
            // skills.dmg를 원본 SkillPower로 사용하며, 대상 방어력이 달라질 때마다 다시 계산합니다.
            return ApplyDamage(BaseDamage(a,b,skill.dmg >= 0 ? skill.dmg : skill.power), b, SkillMultiplier(a, b, skill));
        }
        void Hit(Fighter f, int damage) { f.hp = Math.Max(0, f.hp - damage); }
        bool CanAct(Fighter f, List<BattleMessage> log, bool ai)
        {
            if (f.state == Condition.Confused && random.NextDouble() < .333) {
                int damage = 40; Hit(f, damage);
                string message = Name(f) + "는 혼란으로 기술이 취소되어 자신에게 " + damage + " 물리 피해!";
                log.Add(new BattleMessage(message, ai)); UnityEngine.Debug.Log("[Battle][혼란] " + message);
                return false;
            }
            if (f.state == Condition.Frozen) {
                log.Add(new BattleMessage(Name(f) + "는 얼어서 움직이지 못했다!",ai));
                // 실제로 행동을 막았을 때만 1턴을 소모합니다. 피격한 라운드의 종료로 차감하지 않습니다.
                if (--f.stateTurns <= 0) { f.state = Condition.Ready; f.stateTurns = 0; }
                return false;
            }
            if (f.state == Condition.Paralysis && random.NextDouble() < .15) { log.Add(new BattleMessage(Name(f) + "는 마비로 움직이지 못했다!",ai)); return false; }
            return true;
        }
        void Attack(Fighter a, Fighter b, int skillIndex, bool ai, List<BattleMessage> log)
        {
            a.guard = false;
            if (!CanAct(a,log,ai)) return;
            if (skillIndex == -1) {
                UnityEngine.Debug.Log($"[Battle][라운드 {round}][{(ai ? "적군" : "아군")}] {Name(a)} → {Name(b)} | 스킬: 발버둥");
                int damage = Damage(a,b,2.4f,false); Hit(b,damage); Hit(a,Math.Max(1,a.maxHp / 12));
                log.Add(new BattleMessage(Name(a) + "의 발버둥! " + damage + " 피해 · 반동 " + (a.maxHp / 12), ai, Name(a) + "의 발버둥!")); return;
            }
            var skill = Progress.Data.Skill(Progress.Data.Boo(a.id).skillIds[skillIndex]);
            a.pp[skillIndex]--;
            string skillText = Name(a) + "의 " + skill.name + "!";
            var target = skill.effect == "heal" || skill.effect == "guard" ? a : b;
            UnityEngine.Debug.Log($"[Battle][라운드 {round}][{(ai ? "적군" : "아군")}] {Name(a)} → {Name(target)} | 스킬: {skill.name} (ID: {skill.id}, 효과: {skill.effect}) | 남은 PP: {a.pp[skillIndex]}");
            if (skill.effect == "heal") {
                int heal = Math.Min(a.maxHp - a.hp,(skill.dmg >= 0 ? skill.dmg : (int)(a.maxHp * skill.power))); a.hp += heal;
                a.state = Condition.Ready; a.stateTurns=0;
                UnityEngine.Debug.Log($"[Battle][회복] {skill.name} | 실제 회복:{heal} | HP:{a.hp}/{a.maxHp}");
                log.Add(new BattleMessage(Name(a) + "의 " + skill.name + "! HP +" + heal + " · 상태 회복",ai,skillText)); return;
            }
            if (skill.effect == "guard") { a.guard = true; log.Add(new BattleMessage(Name(a) + "의 " + skill.name + "! 다음 공격을 방어한다.",ai,skillText)); return; }
            int beforeHp=b.hp;
            int amount = Damage(a,b,skill); Hit(b,amount);
            UnityEngine.Debug.Log($"[Battle][피해][{(ai ? "적군" : "아군")}] {skill.name} (ID:{skill.id}) | 위력:{skill.dmg} | Lv:{a.level} ATK:{a.atk} DEF:{b.def} | 상성:{SkillMultiplier(a,b,skill):0.0} | 계산 피해:{amount} | 대상 HP:{beforeHp} → {b.hp}");
            float m = SkillMultiplier(a,b,skill);
            log.Add(new BattleMessage(Name(a) + "의 " + skill.name + "! " + amount + " 피해" + (m > 1 ? " · 효과가 굉장했다!" : m < 1 ? " · 효과가 약했다…" : ""),ai,skillText));
            bool immune = skill.status == (int)Condition.Paralysis && Progress.Data.Boo(b.id).type == (int)Element.Electric;
            if (b.Alive && skill.status > 1 && b.state == Condition.Ready && !immune && random.NextDouble() < skill.chance) {
                b.state = (Condition)skill.status; b.stateTurns = b.state == Condition.Frozen ? random.Next(1,3) : 2;
                log.Add(new BattleMessage(Name(b) + "에게 " + StatusName(b.state) + " 발생!",ai));
            }
        }
        public static string StatusName(Condition state) { return new[] {"", "출전 가능", "마비", "빙결", "화상", "기절", "혼란"}[(int)state]; }
        void Tick(Fighter f, bool ai, List<BattleMessage> log)
        {
            if (!f.Alive || f.state == Condition.Ready || f.state == Condition.Frozen) return;
            if (f.state == Condition.Burn) { int n = Math.Max(1,f.maxHp / 16); Hit(f,n); log.Add(new BattleMessage(Name(f) + " 화상 피해 " + n,ai)); }
            if (--f.stateTurns <= 0) f.state=Condition.Ready;
        }
        bool Resolve(List<BattleMessage> log)
        {
            // A simultaneous knockout is a loss: explicit deterministic tie policy.
            if (!player.Any(x => x.Alive)) { Finished=true; Won=false; log.Add(new BattleMessage("우리 파티가 모두 리타이어했습니다.")); return true; }
            if (!enemy.Any(x => x.Alive)) { Finished=true; Won=true; log.Add(new BattleMessage("상대 파티를 모두 쓰러뜨렸습니다!")); return true; }
            if (!Enemy.Alive) { log.Add(new BattleMessage(Name(Enemy) + " 리타이어!",true)); enemyIndex=Array.FindIndex(enemy,x=>x.Alive); log.Add(new BattleMessage(Stage.npc + "의 " + Name(Enemy) + " 등장!",true)); }
            return false;
        }
        int PickEnemySkill()
        {
            var ids=Progress.Data.Boo(Enemy.id).skillIds;
            var choices=Enumerable.Range(0,3).Where(i=>Enemy.pp[i]>0).ToArray();
            if (choices.Length==0) return -1;
            int heal=Array.FindIndex(ids,id=>Progress.Data.Skill(id).effect=="heal");
            if (heal>=0 && Enemy.pp[heal]>0 && Enemy.hp<Enemy.maxHp*.4) return heal;
            var attacks=choices.Where(i=>Progress.Data.Skill(ids[i]).effect=="attack" || Progress.Data.Skill(ids[i]).effect=="element").ToArray();
            return attacks.Length>0 ? attacks.OrderByDescending(i=>Damage(Enemy,Player,Progress.Data.Skill(ids[i]))).First() : choices[0];
        }
        public List<BattleMessage> Act(int skill)
        {
            var log=new List<BattleMessage>();
            if (Finished || NeedsSwitch || skill < -1 || skill > 2 || (skill == -1 && Player.pp.Any(x=>x>0)) || (skill>=0 && Player.pp[skill]<=0)) return log;
            var actingPlayer = Player; var actingEnemy = Enemy;
            Attack(actingPlayer,actingEnemy,skill,false,log);
            // Defeating an enemy consumes its turn; replacement does not attack immediately.
            bool enemyDown=!Enemy.Alive;
            if (Resolve(log)) return log;
            if (!enemyDown && Player.Alive) Attack(Enemy,Player,PickEnemySkill(),true,log);
            Tick(actingPlayer,false,log); Tick(actingEnemy,true,log);
            Resolve(log); round++; return log;
        }
        public List<BattleMessage> Switch(int index)
        {
            var log=new List<BattleMessage>();
            if (Finished || index<0 || index>=player.Length || index==playerIndex || !player[index].Alive) return log;
            bool free=NeedsSwitch; playerIndex=index;
            log.Add(new BattleMessage(Name(Player) + "! 너로 정했다!"));
            if (!free) { Attack(Enemy,Player,PickEnemySkill(),true,log); Tick(Player,false,log); Tick(Enemy,true,log); Resolve(log); round++; }
            return log;
        }
    }
}
