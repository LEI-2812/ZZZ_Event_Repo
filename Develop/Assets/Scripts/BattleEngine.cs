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
        public BattleMessage(string t, bool e = false) { text = t; enemy = e; }
    }
    public class BattleEngine
    {
        public readonly ProgressService Progress;
        public readonly StageData Stage;
        public readonly Fighter[] player, enemy;
        public int playerIndex, enemyIndex, round = 1;
        public int[] playerExperience, playerNeedExperience;
        public bool Finished, Won;
        readonly Random random;
        public Fighter Player { get { return player[playerIndex]; } }
        public Fighter Enemy { get { return enemy[enemyIndex]; } }
        public bool NeedsSwitch { get { return !Finished && !Player.Alive; } }
        public BattleEngine(ProgressService p, int stage, int seed = -1, PartyEntry[] initialParty = null)
        {
            Progress = p; Stage = p.Data.Stage(stage); random = seed < 0 ? new Random() : new Random(seed);
            if (!p.Eligible || !p.StageOpen(stage) || (initialParty == null ? p.Save.party.Count == 0 : initialParty.Length == 0)) throw new InvalidOperationException("Battle not eligible");
            player = initialParty == null ? p.Save.party.Select(id => Create(id, p.Owned(id).level)).ToArray() : initialParty.Select(row => {
                var fighter = Create(row.bid, row.level > 0 ? row.level : p.Owned(row.bid).level);
                if (row.max_hp > 0) fighter.maxHp = row.max_hp;
                if (row.hp > fighter.maxHp) throw new InvalidOperationException("party hp exceeds max_hp");
                fighter.hp = row.state == 5 ? 0 : row.hp;
                fighter.state = row.state == 5 ? Condition.Ready : (Condition)row.state;
                fighter.stateTurns = fighter.state == Condition.Ready ? 0 : 2;
                return fighter;
            }).ToArray();
            playerExperience = player.Select((f,i) => initialParty == null ? p.Owned(f.id).xp : initialParty[i].current_exp).ToArray();
            playerNeedExperience = player.Select((f,i) => initialParty == null ? p.Data.NeedXp(f.level) : initialParty[i].need_exp).ToArray();
            if (initialParty != null) playerIndex = Array.FindIndex(initialParty, r=>r.is_on_field==1);
            if (!Player.Alive) playerIndex = Array.FindIndex(player, f=>f.Alive);
            enemy = Stage.enemies.Select(id => Create(id, Stage.level)).ToArray();
        }
        Fighter Create(int id, int level)
        {
            var b = Progress.Data.Boo(id);
            int hp = Progress.Stat(b,level,"hp");
            return new Fighter { id=id, level=level, maxHp=hp, hp=hp, atk=Progress.Stat(b,level,"atk"), def=Progress.Stat(b,level,"def"), pp=b.skillIds.Select(x => Progress.Data.Skill(x).charge_count).ToArray() };
        }
        string Name(Fighter f) { return Progress.Data.Boo(f.id).name; }
        public int Damage(Fighter a, Fighter b, float power, bool elemental)
        {
            float mult = elemental ? Elements.Multiplier(Progress.Data.Boo(a.id).type, Progress.Data.Boo(b.id).type, Progress.Data.balance) : 1;
            return Math.Max(1,(int)Math.Round(a.atk * power * mult / (1 + b.def * Progress.Data.balance.defenseFactor) * (b.guard ? .45 : 1)));
        }
        void Hit(Fighter f, int damage) { f.hp = Math.Max(0, f.hp - damage); }
        bool CanAct(Fighter f, List<BattleMessage> log, bool ai)
        {
            if (f.state == Condition.Frozen) { log.Add(new BattleMessage(Name(f) + "는 얼어서 움직이지 못했다!",ai)); f.state = Condition.Ready; f.stateTurns = 0; return false; }
            if (f.state == Condition.Paralysis && random.NextDouble() < .25) { log.Add(new BattleMessage(Name(f) + "는 마비로 움직이지 못했다!",ai)); return false; }
            return true;
        }
        void Attack(Fighter a, Fighter b, int skillIndex, bool ai, List<BattleMessage> log)
        {
            a.guard = false;
            if (!CanAct(a,log,ai)) return;
            if (skillIndex == -1) {
                int damage = Damage(a,b,2.4f,false); Hit(b,damage); Hit(a,Math.Max(1,a.maxHp / 12));
                log.Add(new BattleMessage(Name(a) + "의 발버둥! " + damage + " 피해 · 반동 " + (a.maxHp / 12), ai)); return;
            }
            var skill = Progress.Data.Skill(Progress.Data.Boo(a.id).skillIds[skillIndex]);
            a.pp[skillIndex]--;
            if (skill.effect == "heal") {
                int heal = Math.Min(a.maxHp - a.hp,(int)(a.maxHp * skill.power)); a.hp += heal;
                a.state = Condition.Ready; a.stateTurns=0;
                log.Add(new BattleMessage(Name(a) + "의 " + skill.name + "! HP +" + heal + " · 상태 회복",ai)); return;
            }
            if (skill.effect == "guard") { a.guard = true; log.Add(new BattleMessage(Name(a) + "의 " + skill.name + "! 다음 공격을 방어한다.",ai)); return; }
            bool elemental = skill.effect == "element";
            int amount = Damage(a,b,skill.power,elemental); Hit(b,amount);
            float m = elemental ? Elements.Multiplier(Progress.Data.Boo(a.id).type,Progress.Data.Boo(b.id).type,Progress.Data.balance) : 1;
            log.Add(new BattleMessage(Name(a) + "의 " + skill.name + "! " + amount + " 피해" + (m > 1 ? " · 효과가 굉장했다!" : m < 1 ? " · 효과가 약했다…" : ""),ai));
            if (b.Alive && skill.status > 1 && b.state == Condition.Ready && random.NextDouble() < skill.chance) {
                b.state = (Condition)skill.status; b.stateTurns = 2;
                log.Add(new BattleMessage(Name(b) + "에게 " + StatusName(b.state) + " 발생!",ai));
            }
        }
        public static string StatusName(Condition state) { return new[] {"", "출전 가능", "마비", "빙결", "화상"}[(int)state]; }
        void Tick(Fighter f, bool ai, List<BattleMessage> log)
        {
            if (!f.Alive || f.state == Condition.Ready) return;
            if (f.state == Condition.Burn) { int n = Math.Max(1,f.maxHp * 6 / 100); Hit(f,n); log.Add(new BattleMessage(Name(f) + " 화상 피해 " + n,ai)); }
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
            return attacks.Length>0 ? attacks.OrderByDescending(i=>Damage(Enemy,Player,Progress.Data.Skill(ids[i]).power,Progress.Data.Skill(ids[i]).effect=="element")).First() : choices[0];
        }
        public List<BattleMessage> Act(int skill)
        {
            var log=new List<BattleMessage>();
            if (Finished || NeedsSwitch || skill < -1 || skill > 2 || (skill == -1 && Player.pp.Any(x=>x>0)) || (skill>=0 && Player.pp[skill]<=0)) return log;
            Attack(Player,Enemy,skill,false,log);
            // Defeating an enemy consumes its turn; replacement does not attack immediately.
            bool enemyDown=!Enemy.Alive;
            if (Resolve(log)) return log;
            if (!enemyDown && Player.Alive) Attack(Enemy,Player,PickEnemySkill(),true,log);
            Tick(Player,false,log); Tick(Enemy,true,log);
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
