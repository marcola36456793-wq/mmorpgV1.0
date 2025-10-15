using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MMOServer.Server;
using MMOServer.Models;

namespace MMOServer.Utils
{
    public static class MessageHandler
    {
        public static string? HandleMessage(string message, string sessionId)
        {
            try
            {
                var json = JObject.Parse(message);
                var type = json["type"]?.ToString();

                switch (type)
                {
                    case "login":
                        return HandleLogin(json);

                    case "register":
                        return HandleRegister(json);

                    case "createCharacter":
                        return HandleCreateCharacter(json);

                    case "selectCharacter":
                        return HandleSelectCharacter(json, sessionId);

                    case "moveRequest":
                        return HandleMoveRequest(json, sessionId);

                    case "attackMonster":
                        return HandleAttackMonster(json, sessionId);

                    case "respawnRequest":
                        return HandleRespawnRequest(json, sessionId);
                    
                    case "addStatusPoint":
                        return HandleAddStatusPoint(json, sessionId);

                    case "getInventory":
                        return HandleGetInventory(json, sessionId);

                    case "useItem":
                        return HandleUseItem(json, sessionId);

                    case "equipItem":
                        return HandleEquipItem(json, sessionId);

                    case "unequipItem":
                        return HandleUnequipItem(json, sessionId);

                    case "dropItem":
                        return HandleDropItem(json, sessionId);

                    case "getPlayers":
                        return HandleGetPlayers();

                    case "getMonsters":
                        return HandleGetMonsters();

                    default:
                        return JsonConvert.SerializeObject(new { type = "error", message = "Unknown message type" });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error handling message: {ex.Message}");
                return JsonConvert.SerializeObject(new { type = "error", message = ex.Message });
            }
        }

        private static string HandleLogin(JObject json)
        {
            var username = json["username"]?.ToString();
            var password = json["password"]?.ToString();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return JsonConvert.SerializeObject(new { type = "loginResponse", success = false, message = "Invalid credentials" });
            }

            var response = LoginManager.Instance.Login(username, password);
            return JsonConvert.SerializeObject(new { type = "loginResponse", data = response });
        }

        private static string HandleRegister(JObject json)
        {
            var username = json["username"]?.ToString();
            var password = json["password"]?.ToString();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return JsonConvert.SerializeObject(new { type = "registerResponse", success = false, message = "Invalid data" });
            }

            var success = LoginManager.Instance.Register(username, password);
            return JsonConvert.SerializeObject(new 
            { 
                type = "registerResponse", 
                success = success, 
                message = success ? "Account created successfully" : "Username already exists" 
            });
        }

        private static string HandleCreateCharacter(JObject json)
        {
            var accountId = json["accountId"]?.ToObject<int>() ?? 0;
            var nome = json["nome"]?.ToString();
            var raca = json["raca"]?.ToString();
            var classe = json["classe"]?.ToString();

            if (accountId == 0 || string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(raca) || string.IsNullOrEmpty(classe))
            {
                return JsonConvert.SerializeObject(new { type = "createCharacterResponse", success = false, message = "Invalid data" });
            }

            var character = CharacterManager.Instance.CreateCharacter(accountId, nome, raca, classe);
            
            if (character != null)
            {
                return JsonConvert.SerializeObject(new { type = "createCharacterResponse", success = true, character = character });
            }

            return JsonConvert.SerializeObject(new { type = "createCharacterResponse", success = false, message = "Failed to create character" });
        }

        private static string HandleSelectCharacter(JObject json, string sessionId)
        {
            var characterId = json["characterId"]?.ToObject<int>() ?? 0;

            if (characterId == 0)
            {
                return JsonConvert.SerializeObject(new { type = "selectCharacterResponse", success = false, message = "Invalid character" });
            }

            var character = CharacterManager.Instance.GetCharacter(characterId);
            
            if (character != null)
            {
                var player = new Player
                {
                    sessionId = sessionId,
                    character = character,
                    position = character.position,
                    lastAttackTime = -999f
                };

                PlayerManager.Instance.AddPlayer(sessionId, player);

                var allPlayers = PlayerManager.Instance.GetAllPlayers()
                    .Select(p => new
                    {
                        playerId = p.sessionId,
                        characterName = p.character.nome,
                        position = p.position,
                        raca = p.character.raca,
                        classe = p.character.classe,
                        level = p.character.level,
                        health = p.character.health,
                        maxHealth = p.character.maxHealth,
                        mana = p.character.mana,
                        maxMana = p.character.maxMana,
                        experience = p.character.experience,
                        statusPoints = p.character.statusPoints,
                        isMoving = p.isMoving,
                        targetPosition = p.targetPosition,
                        inCombat = p.inCombat,
                        targetMonsterId = p.targetMonsterId,
                        isDead = p.character.isDead
                    }).ToList();

                var allMonsters = MonsterManager.Instance.GetAllMonsterStates();
                var inventory = ItemManager.Instance.LoadInventory(characterId);

                var newPlayerMessage = JsonConvert.SerializeObject(new
                {
                    type = "playerJoined",
                    player = new
                    {
                        playerId = sessionId,
                        characterName = character.nome,
                        position = character.position,
                        raca = character.raca,
                        classe = character.classe,
                        level = character.level,
                        health = character.health,
                        maxHealth = character.maxHealth
                    }
                });

                Console.WriteLine($"✅ {character.nome} entered the world [ASPD: {character.attackSpeed:F2}s]");

                return "BROADCAST:" + newPlayerMessage + "|||" +
                       JsonConvert.SerializeObject(new 
                       { 
                           type = "selectCharacterResponse", 
                           success = true, 
                           character = character,
                           playerId = sessionId,
                           allPlayers = allPlayers,
                           allMonsters = allMonsters,
                           inventory = inventory
                       });
            }

            return JsonConvert.SerializeObject(new { type = "selectCharacterResponse", success = false, message = "Character not found" });
        }

        private static string HandleMoveRequest(JObject json, string sessionId)
        {
            var targetPosition = json["targetPosition"]?.ToObject<Position>();

            if (targetPosition == null)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Invalid target position" });
            }

            var player = PlayerManager.Instance.GetPlayer(sessionId);
            if (player == null)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Player not found" });
            }

            if (player.inCombat)
            {
                player.CancelCombat();
                Console.WriteLine($"🚶 {player.character.nome} stopped attacking (manual move)");
            }

            var success = PlayerManager.Instance.SetPlayerTarget(sessionId, targetPosition);

            if (success)
            {
                return JsonConvert.SerializeObject(new
                {
                    type = "moveAccepted",
                    targetPosition = targetPosition
                });
            }

            return JsonConvert.SerializeObject(new { type = "error", message = "Failed to set target" });
        }

        private static string HandleAttackMonster(JObject json, string sessionId)
        {
            var monsterId = json["monsterId"]?.ToObject<int>() ?? 0;

            if (monsterId == 0)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Invalid monster ID" });
            }

            var player = PlayerManager.Instance.GetPlayer(sessionId);
            var monster = MonsterManager.Instance.GetMonster(monsterId);

            if (player == null)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Player not found" });
            }

            if (player.character.isDead)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Cannot attack while dead" });
            }

            if (monster == null || !monster.isAlive)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Monster not found or dead" });
            }

            if (player.inCombat && player.targetMonsterId == monsterId)
            {
                Console.WriteLine($"⚠️ {player.character.nome} already attacking {monster.template.name}");
                return JsonConvert.SerializeObject(new
                {
                    type = "attackStarted",
                    monsterId = monsterId,
                    monsterName = monster.template.name,
                    alreadyInCombat = true
                });
            }

            if (player.inCombat && player.targetMonsterId != monsterId)
            {
                Console.WriteLine($"🔄 {player.character.nome} switching target to {monster.template.name}");
            }

            player.inCombat = true;
            player.targetMonsterId = monsterId;
            player.targetPosition = new Position
            {
                x = monster.position.x,
                y = monster.position.y,
                z = monster.position.z
            };
            player.isMoving = true;

            Console.WriteLine($"⚔️ {player.character.nome} started attacking {monster.template.name} [ASPD: {player.character.attackSpeed:F2}s]");

            return JsonConvert.SerializeObject(new
            {
                type = "attackStarted",
                monsterId = monsterId,
                monsterName = monster.template.name,
                alreadyInCombat = false
            });
        }

private static string HandleRespawnRequest(JObject json, string sessionId)
{
    var player = PlayerManager.Instance.GetPlayer(sessionId);

    if (player == null)
    {
        return JsonConvert.SerializeObject(new { type = "error", message = "Player not found" });
    }

    if (!player.character.isDead)
    {
        return JsonConvert.SerializeObject(new { type = "error", message = "Character is not dead" });
    }

    // ✅ CORREÇÃO: GetSpawnPosition já retorna Y ajustado ao terreno
    var spawnPosition = CharacterManager.Instance.GetSpawnPosition(player.character.raca);
    
    // ✅ Adiciona variação aleatória (±1 metro em X e Z)
    Random rand = new Random();
    spawnPosition.x += (float)(rand.NextDouble() * 2 - 1);
    spawnPosition.z += (float)(rand.NextDouble() * 2 - 1);
    
    // ✅ Reajusta Y após adicionar variação (para não cair em buraco)
    TerrainHeightmap.Instance.ClampToGround(spawnPosition, 0f);
    
    // ✅ Atualiza character
    player.character.Respawn(spawnPosition);
    
    // ✅ Atualiza player position
    player.position.x = spawnPosition.x;
    player.position.y = spawnPosition.y; // ✅ Agora Y está correto
    player.position.z = spawnPosition.z;
    
    // ✅ Cancela combate e reseta cooldown
    player.CancelCombat();
    player.lastAttackTime = -999f;

    DatabaseHandler.Instance.UpdateCharacter(player.character);

    Console.WriteLine($"✨ {player.character.nome} ({player.character.raca}) respawned at ({spawnPosition.x:F1}, {spawnPosition.y:F1}, {spawnPosition.z:F1})");

    WorldManager.Instance.BroadcastPlayerRespawn(player);

    return JsonConvert.SerializeObject(new
    {
        type = "respawnResponse",
        success = true,
        position = spawnPosition,
        health = player.character.health,
        maxHealth = player.character.maxHealth
    });
}

        private static string HandleAddStatusPoint(JObject json, string sessionId)
        {
            var stat = json["stat"]?.ToString();

            if (string.IsNullOrEmpty(stat))
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Invalid stat" });
            }

            var player = PlayerManager.Instance.GetPlayer(sessionId);

            if (player == null)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Player not found" });
            }

            if (player.character.statusPoints <= 0)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "No status points available" });
            }

            bool success = player.character.AddStatusPoint(stat);

            if (success)
            {
                DatabaseHandler.Instance.UpdateCharacter(player.character);

                Console.WriteLine($"📈 {player.character.nome} added point to {stat.ToUpper()}");

                var message = new
                {
                    type = "statusPointAdded",
                    playerId = sessionId,
                    characterName = player.character.nome,
                    stat = stat,
                    statusPoints = player.character.statusPoints,
                    newStats = new
                    {
                        strength = player.character.strength,
                        intelligence = player.character.intelligence,
                        dexterity = player.character.dexterity,
                        vitality = player.character.vitality,
                        maxHealth = player.character.maxHealth,
                        maxMana = player.character.maxMana,
                        attackPower = player.character.attackPower,
                        magicPower = player.character.magicPower,
                        defense = player.character.defense,
                        attackSpeed = player.character.attackSpeed
                    }
                };

                return "BROADCAST:" + JsonConvert.SerializeObject(message);
            }

            return JsonConvert.SerializeObject(new { type = "error", message = "Failed to add status point" });
        }

        // ==================== INVENTÁRIO (CORRIGIDO) ====================

        private static string HandleGetInventory(JObject json, string sessionId)
        {
            var player = PlayerManager.Instance.GetPlayer(sessionId);
            
            if (player == null)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Player not found" });
            }

            var inventory = ItemManager.Instance.LoadInventory(player.character.id);

            var itemsData = inventory.items.Select(item => new
            {
                instanceId = item.instanceId,
                templateId = item.templateId,
                quantity = item.quantity,
                slot = item.slot,
                isEquipped = item.isEquipped,
                template = item.template != null ? new
                {
                    id = item.template.id,
                    name = item.template.name,
                    description = item.template.description,
                    type = item.template.type,
                    subType = item.template.subType,
                    slot = item.template.slot,
                    maxStack = item.template.maxStack,
                    iconPath = item.template.iconPath,
                    requiredLevel = item.template.requiredLevel,
                    requiredClass = item.template.requiredClass,
                    effectType = item.template.effectType,
                    effectValue = item.template.effectValue,
                    effectTarget = item.template.effectTarget,
                    bonusStrength = item.template.bonusStrength,
                    bonusIntelligence = item.template.bonusIntelligence,
                    bonusDexterity = item.template.bonusDexterity,
                    bonusVitality = item.template.bonusVitality,
                    bonusMaxHealth = item.template.bonusMaxHealth,
                    bonusMaxMana = item.template.bonusMaxMana,
                    bonusAttackPower = item.template.bonusAttackPower,
                    bonusMagicPower = item.template.bonusMagicPower,
                    bonusDefense = item.template.bonusDefense
                } : null
            }).ToList();

            return JsonConvert.SerializeObject(new
            {
                type = "inventoryResponse",
                success = true,
                inventory = new
                {
                    characterId = inventory.characterId,
                    maxSlots = inventory.maxSlots,
                    gold = inventory.gold,
                    items = itemsData,
                    weaponId = inventory.weaponId,
                    armorId = inventory.armorId,
                    helmetId = inventory.helmetId,
                    bootsId = inventory.bootsId,
                    glovesId = inventory.glovesId,
                    ringId = inventory.ringId,
                    necklaceId = inventory.necklaceId
                }
            });
        }

        // ✅ CORREÇÃO PRINCIPAL: HandleUseItem com validações
        private static string HandleUseItem(JObject json, string sessionId)
        {
            var instanceId = json["instanceId"]?.ToObject<int>() ?? 0;

            if (instanceId == 0)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Invalid item" });
            }

            // ✅ Chama ItemManager.UseItem que retorna string de status
            var result = ItemManager.Instance.UseItem(sessionId, instanceId);
            
            var player = PlayerManager.Instance.GetPlayer(sessionId);

            switch (result)
            {
                case "SUCCESS":
                    if (player != null)
                    {
                        var inventory = ItemManager.Instance.LoadInventory(player.character.id);
                        var item = inventory.GetItem(instanceId);
                        
                        var message = new
                        {
                            type = "itemUsed",
                            playerId = sessionId,
                            instanceId = instanceId,
                            health = player.character.health,
                            maxHealth = player.character.maxHealth,
                            mana = player.character.mana,
                            maxMana = player.character.maxMana,
                            remainingQuantity = item?.quantity ?? 0
                        };

                        return "BROADCAST:" + JsonConvert.SerializeObject(message);
                    }
                    break;

                case "HP_FULL":
                    return JsonConvert.SerializeObject(new 
                    { 
                        type = "itemUseFailed", 
                        reason = "HP_FULL",
                        message = "HP já está cheio!" 
                    });

                case "MP_FULL":
                    return JsonConvert.SerializeObject(new 
                    { 
                        type = "itemUseFailed", 
                        reason = "MP_FULL",
                        message = "MP já está cheio!" 
                    });

                case "ON_COOLDOWN":
                    return JsonConvert.SerializeObject(new 
                    { 
                        type = "itemUseFailed", 
                        reason = "ON_COOLDOWN",
                        message = "Aguarde antes de usar outra poção!" 
                    });

                case "PLAYER_DEAD":
                    return JsonConvert.SerializeObject(new 
                    { 
                        type = "error", 
                        message = "Você não pode usar itens enquanto está morto!" 
                    });

                case "NOT_CONSUMABLE":
                    return JsonConvert.SerializeObject(new 
                    { 
                        type = "error", 
                        message = "Este item não pode ser usado!" 
                    });

                default:
                    return JsonConvert.SerializeObject(new 
                    { 
                        type = "error", 
                        message = "Falha ao usar item" 
                    });
            }

            return JsonConvert.SerializeObject(new { type = "error", message = "Failed to use item" });
        }

        private static string HandleEquipItem(JObject json, string sessionId)
        {
            var instanceId = json["instanceId"]?.ToObject<int>() ?? 0;

            if (instanceId == 0)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Invalid item" });
            }

            var success = ItemManager.Instance.EquipItem(sessionId, instanceId);

            if (success)
            {
                var player = PlayerManager.Instance.GetPlayer(sessionId);
                
                if (player != null)
                {
                    var inventory = ItemManager.Instance.LoadInventory(player.character.id);
                    
                    var message = new
                    {
                        type = "itemEquipped",
                        playerId = sessionId,
                        instanceId = instanceId,
                        newStats = new
                        {
                            strength = player.character.strength,
                            intelligence = player.character.intelligence,
                            dexterity = player.character.dexterity,
                            vitality = player.character.vitality,
                            maxHealth = player.character.maxHealth,
                            maxMana = player.character.maxMana,
                            attackPower = player.character.attackPower,
                            magicPower = player.character.magicPower,
                            defense = player.character.defense,
                            attackSpeed = player.character.attackSpeed
                        },
                        equipment = new
                        {
                            weaponId = inventory.weaponId,
                            armorId = inventory.armorId,
                            helmetId = inventory.helmetId,
                            bootsId = inventory.bootsId,
                            glovesId = inventory.glovesId,
                            ringId = inventory.ringId,
                            necklaceId = inventory.necklaceId
                        }
                    };

                    return "BROADCAST:" + JsonConvert.SerializeObject(message);
                }
            }

            return JsonConvert.SerializeObject(new { type = "error", message = "Failed to equip item" });
        }

// Substituir o método HandleUnequipItem no MessageHandler.cs

private static string HandleUnequipItem(JObject json, string sessionId)
{
    var slot = json["slot"]?.ToString();

    if (string.IsNullOrEmpty(slot))
    {
        Console.WriteLine("❌ HandleUnequipItem: Invalid slot");
        return JsonConvert.SerializeObject(new { type = "error", message = "Invalid slot" });
    }

    Console.WriteLine($"🔧 Unequipping item from slot: {slot} for player: {sessionId}");

    try
    {
        var success = ItemManager.Instance.UnequipItem(sessionId, slot);

        if (success)
        {
            var player = PlayerManager.Instance.GetPlayer(sessionId);
            
            if (player != null)
            {
                var inventory = ItemManager.Instance.LoadInventory(player.character.id);
                
                var message = new
                {
                    type = "itemUnequipped",
                    playerId = sessionId,
                    slot = slot,
                    newStats = new
                    {
                        strength = player.character.strength,
                        intelligence = player.character.intelligence,
                        dexterity = player.character.dexterity,
                        vitality = player.character.vitality,
                        maxHealth = player.character.maxHealth,
                        maxMana = player.character.maxMana,
                        attackPower = player.character.attackPower,
                        magicPower = player.character.magicPower,
                        defense = player.character.defense,
                        attackSpeed = player.character.attackSpeed
                    },
                    equipment = new
                    {
                        weaponId = inventory.weaponId,
                        armorId = inventory.armorId,
                        helmetId = inventory.helmetId,
                        bootsId = inventory.bootsId,
                        glovesId = inventory.glovesId,
                        ringId = inventory.ringId,
                        necklaceId = inventory.necklaceId
                    }
                };

                Console.WriteLine($"✅ Item unequipped from slot {slot} successfully");
                return "BROADCAST:" + JsonConvert.SerializeObject(message);
            }
            else
            {
                Console.WriteLine($"❌ Player not found: {sessionId}");
                return JsonConvert.SerializeObject(new { type = "error", message = "Player not found" });
            }
        }
        else
        {
            Console.WriteLine($"❌ Failed to unequip item from slot {slot}");
            return JsonConvert.SerializeObject(new { type = "error", message = "Failed to unequip item. Slot may be empty." });
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Exception in HandleUnequipItem: {ex.Message}");
        Console.WriteLine($"   StackTrace: {ex.StackTrace}");
        return JsonConvert.SerializeObject(new { type = "error", message = $"Error unequipping item: {ex.Message}" });
    }
}

        private static string HandleDropItem(JObject json, string sessionId)
        {
            var instanceId = json["instanceId"]?.ToObject<int>() ?? 0;
            var quantity = json["quantity"]?.ToObject<int>() ?? 1;

            if (instanceId == 0)
            {
                return JsonConvert.SerializeObject(new { type = "error", message = "Invalid item" });
            }

            var success = ItemManager.Instance.RemoveItemFromPlayer(sessionId, instanceId, quantity);

            if (success)
            {
                var message = new
                {
                    type = "itemDropped",
                    playerId = sessionId,
                    instanceId = instanceId,
                    quantity = quantity
                };

                return "BROADCAST:" + JsonConvert.SerializeObject(message);
            }

            return JsonConvert.SerializeObject(new { type = "error", message = "Failed to drop item" });
        }

        private static string HandleGetPlayers()
        {
            var players = PlayerManager.Instance.GetAllPlayers()
                .Select(p => new
                {
                    playerId = p.sessionId,
                    characterName = p.character.nome,
                    position = p.position,
                    raca = p.character.raca,
                    classe = p.character.classe,
                    level = p.character.level,
                    health = p.character.health,
                    maxHealth = p.character.maxHealth,
                    isMoving = p.isMoving,
                    inCombat = p.inCombat
                }).ToList();

            return JsonConvert.SerializeObject(new { type = "playersResponse", players = players });
        }

        private static string HandleGetMonsters()
        {
            var monsters = MonsterManager.Instance.GetAllMonsterStates();
            return JsonConvert.SerializeObject(new { type = "monstersResponse", monsters = monsters });
        }
    }
}