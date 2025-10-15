using MMOServer.Models;

namespace MMOServer.Server
{
    public class CombatManager
    {
        private static CombatManager? instance;
        public static CombatManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new CombatManager();
                return instance;
            }
        }

        private Random random = new Random();
        
        // ✅ CORREÇÃO #16: Range de ataque estilo Ragnarok (1 célula = ~3 metros)
        private const float ATTACK_RANGE = 3.5f; // 1 célula de range
        private const float CRITICAL_MULTIPLIER = 1.4f; // 140% de dano

        // ==========================================
        // PLAYER ATACA MONSTER
        // ==========================================
        public CombatResult PlayerAttackMonster(Player player, MonsterInstance monster)
        {
            if (player.character.isDead || !monster.isAlive)
            {
                return new CombatResult { damage = 0 };
            }

            // ✅ Verifica range (2D - ignora Y)
            if (!IsInAttackRange(player.position, monster.position, ATTACK_RANGE))
            {
                return new CombatResult { damage = 0 };
            }

            // === CÁLCULO DE HIT (FÓRMULA DO RAGNAROK) ===
            // HIT = 175 + DEX + (BaseLv/10)
            int playerHit = 175 + player.character.dexterity + (player.character.level / 10);
            
            // FLEE do monstro
            int monsterFlee = 100 + monster.template.level + (monster.template.defense * 2);
            
            // Hit Chance = 80% + (HIT - FLEE) / 10
            float hitChance = 0.80f + ((playerHit - monsterFlee) / 100f);
            hitChance = Math.Clamp(hitChance, 0.20f, 0.95f); // Entre 20% e 95%
            
            if (random.NextDouble() > hitChance)
            {
                // MISS!
                return new CombatResult
                {
                    attackerId = player.sessionId,
                    targetId = monster.id.ToString(),
                    attackerType = "player",
                    targetType = "monster",
                    damage = 0,
                    isCritical = false,
                    remainingHealth = monster.currentHealth,
                    targetDied = false
                };
            }

            // === CÁLCULO DE CRITICAL ===
            // CRIT = (1 + (DEX * 0.3 + LUK * 0.3) / 10)%
            // Sem LUK: CRIT = 1% + (DEX * 0.3)%
            float critChance = 0.01f + (player.character.dexterity * 0.003f);
            critChance = Math.Min(critChance, 0.50f); // Max 50%
            
            bool isCritical = random.NextDouble() < critChance;

            // === CÁLCULO DE DANO (RAGNAROK SIMPLIFICADO) ===
            // Damage = (Base ATK + Weapon ATK) * (1 + STR/100)
            int baseATK = player.character.attackPower;
            float strMultiplier = 1.0f + (player.character.strength / 100f);
            int damage = (int)(baseATK * strMultiplier);
            
            // Redução de defesa
            // Damage = Damage * (1 - (DEF / (DEF + 100)))
            int monsterDEF = monster.template.defense;
            float defReduction = 1.0f - (monsterDEF / (float)(monsterDEF + 100));
            damage = (int)(damage * defReduction);
            
            // Variação de dano (±10%)
            float variance = 0.90f + ((float)random.NextDouble() * 0.20f);
            damage = (int)(damage * variance);
            
            // Aplica multiplicador de crítico
            if (isCritical)
            {
                damage = (int)(damage * CRITICAL_MULTIPLIER);
            }
            
            // Dano mínimo de 1
            damage = Math.Max(1, damage);

            // Aplica dano ao monstro
            int actualDamage = monster.TakeDamage(damage);

            // Log de combate no banco
            try
            {
                DatabaseHandler.Instance.LogCombat(player.character.id, monster.id, actualDamage, "physical", isCritical);
            }
            catch { }

            var result = new CombatResult
            {
                attackerId = player.sessionId,
                targetId = monster.id.ToString(),
                attackerType = "player",
                targetType = "monster",
                damage = actualDamage,
                isCritical = isCritical,
                remainingHealth = monster.currentHealth,
                targetDied = !monster.isAlive
            };

            // Se matou o monstro, ganha XP
            if (result.targetDied)
            {
                int expGained = CalculateExperienceReward(player.character.level, monster.template.level, monster.template.experienceReward);
                bool leveledUp = player.character.GainExperience(expGained);

                result.experienceGained = expGained;
                result.leveledUp = leveledUp;
                result.newLevel = player.character.level;

                // Atualiza no banco
                DatabaseHandler.Instance.UpdateCharacter(player.character);
            }

            return result;
        }

        // ==========================================
        // MONSTER ATACA PLAYER
        // ==========================================
        public CombatResult MonsterAttackPlayer(MonsterInstance monster, Player player)
        {
            if (!monster.isAlive || player.character.isDead)
            {
                return new CombatResult { damage = 0 };
            }

            // Verifica range
            if (!IsInAttackRange(monster.position, player.position, ATTACK_RANGE))
            {
                return new CombatResult { damage = 0 };
            }

            // === CÁLCULO DE HIT ===
            int monsterHit = 175 + monster.template.level + (monster.template.attackPower / 5);
            
            // FLEE do player
            // FLEE = 100 + BaseLv + AGI + (AGI/5) + (DEX/10)
            // Simplificado: FLEE = 100 + Level + DEX + (DEX/5)
            int playerFlee = 100 + player.character.level + player.character.dexterity + (player.character.dexterity / 5);
            
            float hitChance = 0.80f + ((monsterHit - playerFlee) / 100f);
            hitChance = Math.Clamp(hitChance, 0.20f, 0.95f);
            
            if (random.NextDouble() > hitChance)
            {
                // MISS!
                return new CombatResult
                {
                    attackerId = monster.id.ToString(),
                    targetId = player.sessionId,
                    attackerType = "monster",
                    targetType = "player",
                    damage = 0,
                    isCritical = false,
                    remainingHealth = player.character.health,
                    targetDied = false
                };
            }

            // Monstros têm chance menor de crítico
            bool isCritical = random.NextDouble() < 0.05f; // 5%

            // === CÁLCULO DE DANO DO MONSTRO ===
            int baseATK = monster.template.attackPower;
            int damage = baseATK;
            
            // Redução de defesa do player
            int playerDEF = player.character.defense;
            float defReduction = 1.0f - (playerDEF / (float)(playerDEF + 100));
            damage = (int)(damage * defReduction);
            
            // Variação
            float variance = 0.90f + ((float)random.NextDouble() * 0.20f);
            damage = (int)(damage * variance);
            
            if (isCritical)
            {
                damage = (int)(damage * CRITICAL_MULTIPLIER);
            }
            
            damage = Math.Max(1, damage);

            // Aplica dano ao player
            int actualDamage = player.character.TakeDamage(damage);

            // Log de combate
            try
            {
                DatabaseHandler.Instance.LogCombat(player.character.id, monster.id, actualDamage, "physical", isCritical);
            }
            catch { }

            var result = new CombatResult
            {
                attackerId = monster.id.ToString(),
                targetId = player.sessionId,
                attackerType = "monster",
                targetType = "player",
                damage = actualDamage,
                isCritical = isCritical,
                remainingHealth = player.character.health,
                targetDied = player.character.isDead
            };

            // Atualiza no banco
            DatabaseHandler.Instance.UpdateCharacter(player.character);

            return result;
        }

        // ==========================================
        // CÁLCULO DE EXPERIÊNCIA (BASEADO NO RO)
        // ==========================================
        private int CalculateExperienceReward(int playerLevel, int monsterLevel, int baseExp)
        {
            int levelDiff = monsterLevel - playerLevel;
            float multiplier = 1.0f;
            
            if (levelDiff <= -10)
            {
                multiplier = 0.10f; // 10% se muito fraco
            }
            else if (levelDiff <= -5)
            {
                multiplier = 0.40f; // 40%
            }
            else if (levelDiff <= -3)
            {
                multiplier = 0.70f; // 70%
            }
            else if (levelDiff >= 10)
            {
                multiplier = 1.50f; // +50% se muito forte
            }
            else if (levelDiff >= 5)
            {
                multiplier = 1.25f; // +25%
            }
            
            return Math.Max(1, (int)(baseExp * multiplier));
        }

        // ==========================================
        // FUNÇÕES AUXILIARES
        // ==========================================
        
        // ✅ CORREÇÃO #17: Distância 2D (ignora Y)
        public bool IsInAttackRange(Position pos1, Position pos2, float range)
        {
            float dx = pos1.x - pos2.x;
            float dz = pos1.z - pos2.z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            
            return distance <= range;
        }

        public bool IsInAggroRange(Position pos1, Position pos2, float aggroRange)
        {
            float dx = pos1.x - pos2.x;
            float dz = pos1.z - pos2.z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            
            return distance <= aggroRange;
        }

        public float GetDistance(Position pos1, Position pos2)
        {
            float dx = pos1.x - pos2.x;
            float dz = pos1.z - pos2.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public float GetAttackRange()
        {
            return ATTACK_RANGE;
        }
    }
}