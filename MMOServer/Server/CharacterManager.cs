using MMOServer.Models;

namespace MMOServer.Server
{
    public class CharacterManager
    {
        private static CharacterManager? instance;
        public static CharacterManager Instance
        {
            get
            {
                if (instance == null)
                    instance = new CharacterManager();
                return instance;
            }
        }

        // ✅ CORREÇÃO: Não define Y aqui, será calculado dinamicamente
        private Dictionary<string, Position> raceSpawnPoints = new Dictionary<string, Position>
        {
            { "Humano", new Position { x = 0, y = 0, z = 0 } },
            { "Elfo", new Position { x = 50, y = 0, z = 50 } },
            { "Anao", new Position { x = -50, y = 0, z = -50 } },
            { "Orc", new Position { x = -50, y = 0, z = 50 } }
        };

        public Character? CreateCharacter(int accountId, string nome, string raca, string classe)
        {
            if (string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(raca) || string.IsNullOrEmpty(classe))
            {
                return null;
            }

            // ✅ CORREÇÃO: Obtém spawn position e ajusta Y ao terreno
            var spawnPos = GetSpawnPosition(raca);

            var classConfig = ConfigManager.Instance.GetClassConfig(classe);
            
            if (classConfig == null)
            {
                Console.WriteLine($"⚠️ Class '{classe}' not found in config, using default stats");
                classConfig = new CharacterClassConfig
                {
                    baseStrength = 10,
                    baseIntelligence = 10,
                    baseDexterity = 10,
                    baseVitality = 10
                };
            }

            var character = new Character
            {
                accountId = accountId,
                nome = nome,
                raca = raca,
                classe = classe,
                position = spawnPos, // ✅ Já vem com Y correto
                level = 1,
                experience = 0,
                statusPoints = 0,
                
                strength = classConfig.baseStrength,
                intelligence = classConfig.baseIntelligence,
                dexterity = classConfig.baseDexterity,
                vitality = classConfig.baseVitality,
                
                isDead = false
            };

            character.RecalculateStats();
            character.health = character.maxHealth;
            character.mana = character.maxMana;

            var characterId = DatabaseHandler.Instance.CreateCharacter(character);
            
            if (characterId > 0)
            {
                character.id = characterId;
                Console.WriteLine($"✅ Character created: {nome} (ID: {characterId}) - {raca} {classe}");
                Console.WriteLine($"   Spawn: ({spawnPos.x:F1}, {spawnPos.y:F1}, {spawnPos.z:F1})");
                Console.WriteLine($"   Base Stats: STR={character.strength} INT={character.intelligence} DEX={character.dexterity} VIT={character.vitality}");
                return character;
            }

            return null;
        }

        public Character? GetCharacter(int characterId)
        {
            return DatabaseHandler.Instance.GetCharacter(characterId);
        }

        // ✅ CORREÇÃO: Calcula Y baseado no terreno
        public Position GetSpawnPosition(string raca)
        {
            Position basePos;
            
            if (raceSpawnPoints.ContainsKey(raca))
            {
                basePos = raceSpawnPoints[raca];
            }
            else
            {
                basePos = new Position { x = 0, y = 0, z = 0 };
            }

            // ✅ Cria nova Position para não modificar o dicionário
            var spawnPos = new Position 
            { 
                x = basePos.x, 
                y = basePos.y, 
                z = basePos.z 
            };

            // ✅ Ajusta Y ao terreno (com offset de 1f para player ficar em pé)
            TerrainHeightmap.Instance.ClampToGround(spawnPos, 0f);

            Console.WriteLine($"📍 Spawn for {raca}: ({spawnPos.x:F1}, {spawnPos.y:F1}, {spawnPos.z:F1})");
            
            return spawnPos;
        }
    }
}