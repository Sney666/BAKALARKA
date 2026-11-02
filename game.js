const SVGs = {
    viking: `<svg viewBox="0 0 100 100" class="avatar"><path d="M15,95 Q50,60 85,95" stroke="#b8860b" stroke-width="4" fill="#4682b4" /><path d="M30,50 L50,85 L70,50 Z" fill="#d2691e" /><path d="M25,50 C25,20 75,20 75,50 Z" fill="#4682b4" stroke="#b8860b" stroke-width="3"/><circle cx="40" cy="55" r="4" fill="#fff"/><circle cx="60" cy="55" r="4" fill="#fff"/><circle cx="40" cy="55" r="1.5" fill="#000"/><circle cx="60" cy="55" r="1.5" fill="#000"/><path d="M25,45 Q5,30 10,10 Q25,25 35,35" fill="#daa520" stroke="#000" stroke-width="1"/><path d="M75,45 Q95,30 90,10 Q75,25 65,35" fill="#daa520" stroke="#000" stroke-width="1"/></svg>`,
    pawn: `<svg viewBox="0 0 100 100" class="avatar"><path d="M20,95 Q50,65 80,95" stroke="#444" stroke-width="4" fill="#777" /><circle cx="50" cy="40" r="20" fill="#999" stroke="#333" stroke-width="2"/><circle cx="35" cy="70" r="15" fill="#654321" stroke="#ccc" stroke-width="2"/><path d="M65,50 L90,20 L80,10 Z" fill="#aaa" stroke="#333" stroke-width="1"/></svg>`,
    archer: `<svg viewBox="0 0 100 100" class="avatar"><path d="M25,95 Q50,60 75,95" stroke="#222" stroke-width="3" fill="#2e8b57" /><path d="M50,15 Q30,30 30,50 Q30,65 50,75 Q70,65 70,50 Q70,30 50,15 Z" fill="#228b22" stroke="#111" stroke-width="2"/><circle cx="42" cy="45" r="3" fill="#fff"/><circle cx="58" cy="45" r="3" fill="#fff"/><path d="M70,30 Q90,50 70,80" fill="none" stroke="#8b4513" stroke-width="4"/><line x1="70" y1="30" x2="70" y2="80" stroke="#fff" stroke-width="1"/></svg>`,
    spearman: `<svg viewBox="0 0 100 100" class="avatar"><path d="M15,95 Q50,70 85,95" stroke="#333" stroke-width="4" fill="#696969" /><rect x="35" y="25" width="30" height="35" rx="5" fill="#a9a9a9" stroke="#444" stroke-width="2"/><rect x="40" y="30" width="20" height="10" fill="#222"/><line x1="80" y1="95" x2="80" y2="10" stroke="#8b4513" stroke-width="4"/><polygon points="75,10 85,10 80,0" fill="#ccc" stroke="#555" stroke-width="1"/></svg>`,
    shaman: `<svg viewBox="0 0 100 100" class="avatar"><path d="M20,95 Q50,50 80,95" stroke="#222" stroke-width="3" fill="#4b0082" /><rect x="35" y="25" width="30" height="35" rx="8" fill="#8b4513" stroke="#333" stroke-width="2"/><circle cx="42" cy="40" r="4" fill="#00ff00"/><circle cx="58" cy="40" r="4" fill="#00ff00"/><path d="M25,60 L20,75 L30,75 Z" fill="#00fa9a" stroke="#000" stroke-width="1"/></svg>`,
    boss: `<svg viewBox="0 0 100 100" class="avatar" style="width:65px; height:65px; margin-top:-10px;"><path d="M10,95 Q50,40 90,95" stroke="#daa520" stroke-width="5" fill="#8b0000" /><path d="M25,40 C25,10 75,10 75,40 Z" fill="#333" stroke="#daa520" stroke-width="3"/><path d="M30,30 L40,10 L50,25 L60,10 L70,30 Z" fill="#ffd700" stroke="#b8860b" stroke-width="2"/><circle cx="38" cy="45" r="4" fill="#f00"/><circle cx="62" cy="45" r="4" fill="#f00"/><path d="M85,50 Q95,30 85,10 Z" fill="#aaa" stroke="#000" stroke-width="2"/><path d="M20,50 Q10,70 5,90 Z" fill="none" stroke="#8b4513" stroke-width="4"/><path d="M5,90 L0,75 L15,80 Z" fill="#ccc" stroke="#000"/></svg>`,
    trap: `<svg viewBox="0 0 100 100" class="avatar"><circle cx="50" cy="50" r="40" fill="none" stroke="#555" stroke-width="4"/><path d="M50,10 L40,30 L60,30 Z M50,90 L40,70 L60,70 Z M10,50 L30,40 L30,60 Z M90,50 L70,40 L70,60 Z" fill="#777"/></svg>`
};

const CARD_DB = [
    { id: 1, name: "Viking Axe", type: "Melee", baseDmg: 3, baseCd: 4, aim: 'ortho', desc: "Zásah 1 sousedního pole (ortogonálně)." },
    { id: 2, name: "Wooden Club", type: "Melee", baseDmg: 2, baseCd: 5, aim: 'ortho', desc: "Zásah a odhození o 1 pole." },
    { id: 3, name: "Two-handed Hammer", type: "Melee", baseDmg: 4, baseCd: 6, aim: 'ortho', desc: "Zásah 3 polí v oblouku." },
    { id: 4, name: "Throwing Axe", type: "Ranged", baseDmg: 2, baseCd: 3, aim: 'ortho', desc: "Projektil rovně, zasáhne prvního." },
    { id: 5, name: "Hunting Bow", type: "Ranged", baseDmg: 3, baseCd: 5, aim: 'diag', desc: "Projektil diagonálně, zasáhne prvního." },
    { id: 6, name: "Heavy Spear", type: "Ranged", baseDmg: 4, baseCd: 6, aim: 'ortho', desc: "Projme 2 nepřátele v řadě." },
    { id: 7, name: "Bear Trap", type: "Special", baseDmg: 2, baseCd: 6, aim: 'self', desc: "Položí past na tvé pole." },
    { id: 8, name: "Boat Hook", type: "Special", baseDmg: 1, baseCd: 5, aim: 'ortho', desc: "Přitáhne cíl na sousední pole." },
    { id: 9, name: "Diagonal Dash", type: "Movement", baseDmg: 0, baseCd: 4, aim: 'diag', desc: "Přesun 1 pole diagonálně." },
    { id: 10, name: "Slide", type: "Movement", baseDmg: 0, baseCd: 5, aim: 'ortho', desc: "Přesun 2 pole rovně." }
];

// Odemknuté od začátku
const INITIAL_CARDS = [1, 4, 7, 9];

// Fronta odemykání zbraní
const CARD_UNLOCK_QUEUE = [
    { id: 2, price: 15 }, { id: 5, price: 20 }, { id: 8, price: 25 }, 
    { id: 10, price: 25 }, { id: 3, price: 30 }, { id: 6, price: 35 }
];

// Fronta odemykání vylepšení
const UPGRADE_QUEUE = [
    { id: 'u1', name: 'Durability Training I', desc: '+1 max HP', price: 20 },
    { id: 'u2', name: 'Swift Boots', desc: '-1 CD pro Movement', price: 30 },
    { id: 'u3', name: 'Charisma', desc: '1. reroll v každé hře zdarma', price: 40 },
    { id: 'u4', name: 'Melee Training I', desc: '+1 dmg pro Melee zbraně', price: 50 },
    { id: 'u5', name: 'Tactical Master', desc: 'Akce Čekat snižuje CD navíc', price: 60 },
    { id: 'u6', name: 'Ranged Training I', desc: '+1 dmg pro Ranged zbraně', price: 70 },
    { id: 'u7', name: 'Speciality Master', desc: 'Viking Scream zraní (1 dmg)', price: 80 }
];

const WAVES = [ [], 
    ['pawn'], ['pawn', 'archer'], ['spearman', 'pawn'], ['spearman', 'archer'], ['pawn', 'pawn', 'shaman'], 
    ['spearman', 'archer', 'archer'], ['pawn', 'pawn', 'pawn', 'archer', 'archer'], ['pawn', 'pawn', 'spearman', 'shaman'], 
    ['pawn', 'shaman', 'spearman', 'archer', 'archer'], ['boss']
];

class Game {
    constructor() {
        this.saveData = this.loadSave();
        this.width = 5; this.height = 5;
        this.state = 'MENU';
        this.bindInput();
        this.openMenu();
    }

    // --- Ukládání a Načítání (Plně zpětně kompatibilní) ---
    loadSave() {
        const saved = localStorage.getItem('rogueCardsSave');
        if (saved) {
            try {
                let parsed = JSON.parse(saved);
                // Bezpečnostní pojistky pro starší nebo poškozené savy
                if (!parsed.unlockedCards || !Array.isArray(parsed.unlockedCards)) {
                    parsed.unlockedCards = [...INITIAL_CARDS];
                }
                if (!parsed.unlockedUpgrades || !Array.isArray(parsed.unlockedUpgrades)) {
                    parsed.unlockedUpgrades = [];
                }
                if (typeof parsed.coins !== 'number') {
                    parsed.coins = 0;
                }
                return parsed;
            } catch (e) {
                console.error("Záchrana starého save souboru selhala, vytvářím nový.", e);
                return { coins: 0, unlockedCards: [...INITIAL_CARDS], unlockedUpgrades: [] };
            }
        }
        return { coins: 0, unlockedCards: [...INITIAL_CARDS], unlockedUpgrades: [] };
    }
    saveGame() { localStorage.setItem('rogueCardsSave', JSON.stringify(this.saveData)); }
    hasUpgrade(uid) { return this.saveData.unlockedUpgrades.includes(uid); }

    // --- Obrazovky (Menu / Shop) ---
    switchScreen(screenId) {
        document.querySelectorAll('.screen').forEach(s => { s.classList.remove('active'); s.classList.add('hidden'); });
        document.getElementById(screenId).classList.remove('hidden');
        document.getElementById(screenId).classList.add('active');
    }
    openMenu() {
        this.state = 'MENU';
        document.getElementById('menu-coins').innerText = this.saveData.coins;
        this.switchScreen('main-menu');
    }
    openShop() {
        this.state = 'SHOP';
        this.renderShop();
        this.switchScreen('shop-screen');
    }
    renderShop() {
        document.getElementById('shop-coins').innerText = this.saveData.coins;
        
        // Zbraně (První 2 zamčené)
        const lockedCards = CARD_UNLOCK_QUEUE.filter(c => !this.saveData.unlockedCards.includes(c.id)).slice(0, 2);
        const cardsCont = document.getElementById('shop-cards'); cardsCont.innerHTML = '';
        if (lockedCards.length === 0) cardsCont.innerHTML = '<div style="color:#aaa;">Všechny zbraně zakoupeny.</div>';
        lockedCards.forEach(item => {
            let cardData = CARD_DB.find(c => c.id === item.id);
            cardsCont.innerHTML += `
                <div class="shop-item">
                    <div class="shop-item-info">
                        <div class="shop-item-name">${cardData.name} <span style="font-size:0.8rem; color:#888;">(${cardData.type})</span></div>
                        <div class="shop-item-desc">${cardData.desc}</div>
                    </div>
                    <button class="buy-btn" ${this.saveData.coins < item.price ? 'disabled' : ''} onclick="game.buyCard(${item.id}, ${item.price})">${item.price} 🪙</button>
                </div>
            `;
        });

        // Upgrady (První 2 zamčené)
        const lockedUpgrades = UPGRADE_QUEUE.filter(u => !this.saveData.unlockedUpgrades.includes(u.id)).slice(0, 2);
        const upgCont = document.getElementById('shop-upgrades'); upgCont.innerHTML = '';
        if (lockedUpgrades.length === 0) upgCont.innerHTML = '<div style="color:#aaa;">Všechna vylepšení zakoupena.</div>';
        lockedUpgrades.forEach(u => {
            upgCont.innerHTML += `
                <div class="shop-item">
                    <div class="shop-item-info">
                        <div class="shop-item-name">${u.name}</div>
                        <div class="shop-item-desc">${u.desc}</div>
                    </div>
                    <button class="buy-btn" ${this.saveData.coins < u.price ? 'disabled' : ''} onclick="game.buyUpgrade('${u.id}', ${u.price})">${u.price} 🪙</button>
                </div>
            `;
        });
    }
    buyCard(id, price) {
        if (this.saveData.coins >= price) { this.saveData.coins -= price; this.saveData.unlockedCards.push(id); this.saveGame(); this.renderShop(); }
    }
    buyUpgrade(id, price) {
        if (this.saveData.coins >= price) { this.saveData.coins -= price; this.saveData.unlockedUpgrades.push(id); this.saveGame(); this.renderShop(); }
    }

    // --- Inicializace Hry ---
    startNewGame() {
        this.wave = 1;
        this.player = { x: 2, y: 2, hp: 5, maxHp: 5, coins: 0, name: "Viking", screamCd: 0, maxScreamCd: 6 };
        // Aplikace Upgrade u1 (Max HP)
        if (this.hasUpgrade('u1')) this.player.maxHp += 1;
        this.player.hp = this.player.maxHp;

        this.enemies = []; this.enemyIdCounter = 1; this.traps = [];
        this.deck = []; this.hand = []; this.prepared = []; this.maxPrepared = 4;
        this.draftSelection = []; this.aimingIndex = 0; this.aimTargetCells = [];
        this.hoverX = -1; this.hoverY = -1;
        this.gameRerolls = 0; // Pro Upgrade u3 (Charisma)
        
        this.switchScreen('game-wrapper');
        this.startDraft();
    }

    createCardInstance(dbCard) { 
        let card = { ...dbCard, cd: 0, uid: Math.random(), mods: [] }; 
        // Aplikace permanentních upgradů
        if (this.hasUpgrade('u2') && card.type === 'Movement') card.baseCd = Math.max(1, card.baseCd - 1);
        if (this.hasUpgrade('u4') && card.type === 'Melee') card.baseDmg += 1;
        if (this.hasUpgrade('u6') && card.type === 'Ranged') card.baseDmg += 1;
        return card;
    }
    getDmg(card) { return card.baseDmg + card.mods.filter(m=>m.type==='dmg').reduce((a,b)=>a+b.val, 0); }
    getMaxCd(card) { let val = card.baseCd + card.mods.filter(m=>m.type==='cd').reduce((a,b)=>a+b.val, 0); return Math.max(1, val); }
    hasPoison(card) { return card.mods.some(m=>m.type==='poison'); }

    // --- SETUP VLN ---
    startWave(waveNum) {
        this.wave = waveNum; 
        this.enemies = []; 
        this.traps = [];
        
        WAVES[waveNum].forEach(type => this.spawnEnemy(type, type==='archer' || type==='shaman'));
        
        this.state = 'PLAY'; 
        this.render();
    }

    spawnEnemy(type, edgeOnly) {
        let cells = [];
        for(let y = 0; y < this.height; y++) {
            for(let x = 0; x < this.width; x++) {
                if (this.player.x === x && this.player.y === y) continue;
                if (this.getEnemyAt(x, y)) continue;
                
                if (edgeOnly) {
                    const isEdge = (x === 0 || x === this.width - 1 || y === 0 || y === this.height - 1);
                    if (!isEdge) continue;
                }
                cells.push({x, y});
            }
        }
        
        // Záchranný fallback: pokud na okraji není místo, povol všechny volné buňky
        if (cells.length === 0 && edgeOnly) {
            for(let y = 0; y < this.height; y++) {
                for(let x = 0; x < this.width; x++) {
                    if (this.player.x === x && this.player.y === y) continue;
                    if (this.getEnemyAt(x, y)) continue;
                    cells.push({x, y});
                }
            }
        }
        
        if (cells.length === 0) return; // Mapa je kriticky plná
        
        const pos = cells[Math.floor(Math.random() * cells.length)];
        
        let enemy = { id: this.enemyIdCounter++, x: pos.x, y: pos.y, type: type, state: 'EQUIPPING', stunned: false, aimData: null, poison: 0 };
        if (type === 'pawn') { enemy.hp = 3; enemy.maxHp = 3; enemy.coins = 2; enemy.name = 'Pawn'; enemy.svg = SVGs.pawn; }
        else if (type === 'archer') { enemy.hp = 2; enemy.maxHp = 2; enemy.coins = 3; enemy.name = 'Archer'; enemy.svg = SVGs.archer; }
        else if (type === 'spearman') { enemy.hp = 4; enemy.maxHp = 4; enemy.coins = 3; enemy.name = 'Spearman'; enemy.svg = SVGs.spearman; }
        else if (type === 'shaman') { enemy.hp = 3; enemy.maxHp = 3; enemy.coins = 4; enemy.name = 'Shaman'; enemy.svg = SVGs.shaman; }
        else if (type === 'boss') { enemy.hp = 30; enemy.maxHp = 30; enemy.coins = 30; enemy.name = 'Viking Chief'; enemy.svg = SVGs.boss; enemy.bossPhase = 0; enemy.bossPrep = 0; enemy.state = 'READY'; }
        
        this.enemies.push(enemy);
    }

    // --- REWARDS & LOGIKA VLN ---
    checkWaveClear() {
        // Zabrání spuštění logiky během Draftu nebo Odměn
        if (this.state !== 'PLAY') return; 
        
        if (this.enemies.length === 0) {
            this.state = 'TRANSITION'; // Pojistka proti zacyklení
            
            if (this.wave === 10) { 
                this.victory(); 
                return; 
            }
            
            if ([2, 6].includes(this.wave)) {
                this.initRewardDraft();
            } else if ([4, 8].includes(this.wave)) {
                this.initRewardUpgrade();
            } else {
                this.startWave(this.wave + 1);
            }
        }
    }
    
    getRerollCost() {
        if (this.hasUpgrade('u3') && this.gameRerolls === 0) return 0;
        return this.rerollBaseCost;
    }

    initRewardDraft() {
        this.state = 'REWARD_DRAFT'; this.rerollBaseCost = 5; this.draftSelection = [];
        this.generateRewardCards();
        document.getElementById('reward-draft-overlay').classList.remove('hidden');
        this.renderRewardDraftUI();
    }
    generateRewardCards() {
        const ownedIds = this.hand.map(c => c.id);
        const avail = CARD_DB.filter(c => this.saveData.unlockedCards.includes(c.id) && !ownedIds.includes(c.id));
        const c1Pool = avail.filter(c => ['Melee', 'Ranged'].includes(c.type));
        const c2Pool = avail.filter(c => ['Movement', 'Special'].includes(c.type));
        
        let c1 = c1Pool.length > 0 ? c1Pool[Math.floor(Math.random() * c1Pool.length)] : (avail.length > 0 ? avail[0] : null);
        let c2 = c2Pool.length > 0 ? c2Pool[Math.floor(Math.random() * c2Pool.length)] : (avail.length > 1 ? avail[1] : null);
        
        this.currentRewardCards = [];
        if (c1) this.currentRewardCards.push(this.createCardInstance(c1));
        if (c2 && c1 !== c2) this.currentRewardCards.push(this.createCardInstance(c2));
    }
    renderRewardDraftUI() {
        const cont = document.getElementById('reward-draft-cards'); cont.innerHTML = '';
        this.currentRewardCards.forEach((c, i) => { cont.appendChild(this.createCardElement(c, 'reward-draft', i)); });
        document.getElementById('btn-reward-draft-confirm').disabled = this.draftSelection.length !== 1;
        
        const btnRR = document.getElementById('btn-reward-draft-reroll');
        let cost = this.getRerollCost();
        btnRR.innerText = `Reroll (${cost} 🪙)`;
        btnRR.disabled = this.player.coins < cost;
    }
    selectRewardDraftCard(index) {
        this.draftSelection = [index];
        Array.from(document.getElementById('reward-draft-cards').children).forEach((el, i) => el.classList.toggle('selected-draft', i === index));
        document.getElementById('btn-reward-draft-confirm').disabled = false;
    }
    claimRewardDraft() {
        if(this.draftSelection.length === 1) this.hand.push(this.currentRewardCards[this.draftSelection[0]]);
        document.getElementById('reward-draft-overlay').classList.add('hidden');
        this.startWave(this.wave + 1);
    }
    rerollRewardDraft() {
        let cost = this.getRerollCost();
        if (this.player.coins >= cost) {
            this.player.coins -= cost; this.gameRerolls++;
            if (cost > 0) this.rerollBaseCost *= 2;
            this.draftSelection = []; this.generateRewardCards(); this.renderRewardDraftUI(); this.renderTopBar();
        }
    }

    initRewardUpgrade() {
        this.state = 'REWARD_UPGRADE'; this.rerollBaseCost = 5;
        this.generateUpgradeMod();
        document.getElementById('reward-upgrade-overlay').classList.remove('hidden');
        this.renderRewardUpgradeUI();
    }
    generateUpgradeMod() {
        const mods = [ { type: 'dmg', val: 1, text: '+1 Dmg' }, { type: 'dmg', val: 2, text: '+2 Dmg' }, { type: 'cd', val: -1, text: '-1 CD' }, { type: 'cd', val: -2, text: '-2 CD' }, { type: 'poison', val: 1, text: 'Otravné (+1 CD)' } ];
        this.currentModifier = mods[Math.floor(Math.random() * mods.length)];
    }
    renderRewardUpgradeUI() {
        document.getElementById('upgrade-mod-box').innerText = `Modifikátor: ${this.currentModifier.text}`;
        const cont = document.getElementById('reward-hand-cards'); cont.innerHTML = '';
        this.hand.forEach((c, i) => { cont.appendChild(this.createCardElement(c, 'reward-upgrade', i)); });
        
        const btnRR = document.getElementById('btn-reward-upgrade-reroll');
        let cost = this.getRerollCost();
        btnRR.innerText = `Reroll (${cost} 🪙)`;
        btnRR.disabled = this.player.coins < cost;
    }
    applyUpgradeToCard(uid) {
        let card = this.hand.find(c => c.uid === uid);
        if (card) {
            if (this.currentModifier.type === 'poison') card.mods.push({type: 'cd', val: 1}); 
            card.mods.push(this.currentModifier);
            document.getElementById('reward-upgrade-overlay').classList.add('hidden');
            this.startWave(this.wave + 1);
        }
    }
    rerollRewardUpgrade() {
        let cost = this.getRerollCost();
        if (this.player.coins >= cost) {
            this.player.coins -= cost; this.gameRerolls++;
            if (cost > 0) this.rerollBaseCost *= 2;
            this.generateUpgradeMod(); this.renderRewardUpgradeUI(); this.renderTopBar();
        }
    }
    skipReward() {
        document.getElementById('reward-draft-overlay').classList.add('hidden');
        document.getElementById('reward-upgrade-overlay').classList.add('hidden');
        this.startWave(this.wave + 1);
    }

    // --- START DRAFT ---
    startDraft() {
        this.state = 'DRAFT';
        let pool = CARD_DB.filter(c => this.saveData.unlockedCards.includes(c.id)).sort(() => 0.5 - Math.random());
        this.deck = pool.slice(0, 4).map(c => this.createCardInstance(c));
        const cont = document.getElementById('draft-cards'); cont.innerHTML = '';
        this.deck.forEach((c, i) => cont.appendChild(this.createCardElement(c, 'draft', i)));
        document.getElementById('draft-overlay').classList.remove('hidden');
    }
    toggleDraftCard(index) {
        let idx = this.draftSelection.indexOf(index);
        if (idx > -1) this.draftSelection.splice(idx, 1); else if (this.draftSelection.length < 2) this.draftSelection.push(index);
        Array.from(document.getElementById('draft-cards').children).forEach((el, i) => el.classList.toggle('selected-draft', this.draftSelection.includes(i)));
        document.getElementById('btn-confirm-draft').disabled = this.draftSelection.length !== 2;
    }
    confirmDraft() {
        this.draftSelection.forEach(idx => this.hand.push({...this.deck[idx]}));
        document.getElementById('draft-overlay').classList.add('hidden');
        this.startWave(1);
    }

    // --- VSTUPY A AKCE ---
    bindInput() {
        document.addEventListener('keydown', (e) => {
            if (this.state !== 'PLAY') return;
            let dx = 0, dy = 0;
            if (e.key === 'ArrowUp' || e.key.toLowerCase() === 'w') dy = -1;
            else if (e.key === 'ArrowDown' || e.key.toLowerCase() === 's') dy = 1;
            else if (e.key === 'ArrowLeft' || e.key.toLowerCase() === 'a') dx = -1;
            else if (e.key === 'ArrowRight' || e.key.toLowerCase() === 'd') dx = 1;
            if (dx !== 0 || dy !== 0) this.tryMovePlayer(dx, dy);
        });
    }
    handleCellHover(x, y) {
        if (this.state === 'AIMING' && (this.hoverX !== x || this.hoverY !== y)) {
            this.hoverX = x; this.hoverY = y; this.calculateAimPreview(); this.renderGridOnly();
        }
    }
    handleCellClick(x, y) {
        if (this.state === 'PLAY') {
            const dx = x - this.player.x, dy = y - this.player.y;
            if (Math.abs(dx) + Math.abs(dy) === 1) this.tryMovePlayer(dx, dy);
        } else if (this.state === 'AIMING') {
            if (this.aimTargetCells.some(c => c.x === x && c.y === y) || (this.hand[0]?.aim === 'self' && x === this.player.x && y === this.player.y)) {
                this.executeAim(x, y);
            }
        }
    }
    tryMovePlayer(dx, dy) {
        const nx = this.player.x + dx, ny = this.player.y + dy;
        if (!this.isValid(nx, ny) || this.getEnemyAt(nx, ny)) return;
        this.player.x = nx; this.player.y = ny; this.endTurn();
    }
    wait() { 
        if (this.state === 'PLAY') {
            // Aplikace Upgrade u5 (Tactical Master)
            if (this.hasUpgrade('u5')) { this.hand.forEach(c => { if (c.cd > 0) c.cd--; }); }
            this.endTurn();
        } 
    }
    vikingScream() {
        if (this.state !== 'PLAY' || this.player.screamCd > 0) return;
        this.enemies.forEach(e => {
            const dx = e.x - this.player.x, dy = e.y - this.player.y;
            if (Math.abs(dx) <= 1 && Math.abs(dy) <= 1) {
                const nx = e.x + Math.sign(dx), ny = e.y + Math.sign(dy);
                if (this.isValid(nx, ny) && !this.getEnemyAt(nx, ny) && !(nx===this.player.x && ny===this.player.y)) { 
                    e.x = nx; e.y = ny; 
                }
                // Aplikace Upgrade u7 (Speciality Master)
                if (this.hasUpgrade('u7')) { this.damageEntityAt(e.x, e.y, 1, false); }
            }
        });
        this.player.screamCd = this.player.maxScreamCd; this.endTurn();
    }
    equipCard(uid) {
        if (this.state !== 'PLAY' || this.prepared.length >= this.maxPrepared) return;
        const idx = this.hand.findIndex(c => c.uid === uid);
        if (idx === -1 || this.hand[idx].cd > 0) return;
        this.prepared.push(this.hand.splice(idx, 1)[0]); this.endTurn();
    }

    // --- MÍŘENÍ ---
    startExecution() { if (this.state === 'PLAY' && this.prepared.length > 0) { this.state = 'AIMING'; this.aimingIndex = 0; this.setupNextAim(); } }
    setupNextAim() {
        if (this.aimingIndex >= this.prepared.length) { this.state = 'PLAY'; this.prepared = []; this.endTurn(); return; }
        const card = this.prepared[this.aimingIndex];
        this.aimTargetCells = card.aim === 'self' ? [{x: this.player.x, y: this.player.y}] : [];
        this.render();
    }
    getDirection(hx, hy, aimType) {
        let dx = hx - this.player.x, dy = hy - this.player.y;
        if (dx === 0 && dy === 0) return null;
        if (aimType === 'ortho') { if (Math.abs(dx) > Math.abs(dy)) { dy=0; dx=Math.sign(dx); } else { dx=0; dy=Math.sign(dy); } }
        else if (aimType === 'diag') { dx = dx===0?1:Math.sign(dx); dy = dy===0?1:Math.sign(dy); }
        return { dx, dy };
    }
    calculateAimPreview() {
        this.aimTargetCells = []; const card = this.prepared[this.aimingIndex];
        if (!card || card.aim === 'self') return;
        const dir = this.getDirection(this.hoverX, this.hoverY, card.aim); if (!dir) return;
        const px = this.player.x, py = this.player.y, dx = dir.dx, dy = dir.dy;

        switch (card.id) {
            case 1: case 2: case 9: this.addAimCell(px+dx, py+dy); break;
            case 3: this.addAimCell(px+dx, py+dy); if(dx!==0){this.addAimCell(px+dx,py-1); this.addAimCell(px+dx,py+1);}else{this.addAimCell(px-1,py+dy); this.addAimCell(px+1,py+dy);} break;
            case 4: case 5: for(let i=1; i<=5; i++) { let nx=px+dx*i, ny=py+dy*i; if(!this.addAimCell(nx, ny) || this.getEnemyAt(nx,ny)) break; } break;
            case 6: let hits=0; for(let i=1; i<=5; i++) { let nx=px+dx*i, ny=py+dy*i; if(!this.addAimCell(nx, ny)) break; if(this.getEnemyAt(nx,ny)) hits++; if(hits>=2) break; } break;
            case 8: for(let i=1; i<=3; i++) { let nx=px+dx*i, ny=py+dy*i; if(!this.addAimCell(nx, ny) || this.getEnemyAt(nx,ny)) break; } break;
            case 10: for(let i=1; i<=2; i++) { let nx=px+dx*i, ny=py+dy*i; if(this.isValid(nx,ny) && !this.getEnemyAt(nx,ny)) this.addAimCell(nx,ny); else break; } break;
        }
    }
    addAimCell(x, y) { if (this.isValid(x, y)) { this.aimTargetCells.push({x, y}); return true; } return false; }
    isValid(x, y) { return x>=0 && x<this.width && y>=0 && y<this.height; }
    getEnemyAt(x, y) { return this.enemies.find(e => e.x === x && e.y === y); }

    executeAim(tx, ty) {
        const card = this.prepared[this.aimingIndex];
        const dir = this.getDirection(tx, ty, card.aim) || {dx:0, dy:0};
        const dx = dir.dx, dy = dir.dy, px = this.player.x, py = this.player.y;
        const dmgCells = [...this.aimTargetCells];
        const dmgAmt = this.getDmg(card);
        const pMod = this.hasPoison(card);

        if ([1, 3, 4, 5, 6].includes(card.id)) { dmgCells.forEach(c => this.damageEntityAt(c.x, c.y, dmgAmt, pMod)); }
        else if (card.id === 2) {
            let e = this.getEnemyAt(px+dx, py+dy);
            if (e) {
                this.damageEntityAt(e.x, e.y, dmgAmt, pMod);
                if (e.hp > 0 && this.isValid(e.x+dx, e.y+dy) && !this.getEnemyAt(e.x+dx, e.y+dy) && !(e.x+dx===this.player.x && e.y+dy===this.player.y)) { e.x+=dx; e.y+=dy; }
            }
        }
        else if (card.id === 7) { if (!this.traps.some(t=>t.x===px && t.y===py)) this.traps.push({x:px, y:py}); }
        else if (card.id === 8) {
            let target = dmgCells[dmgCells.length-1];
            if (target) {
                let e = this.getEnemyAt(target.x, target.y);
                if (e) { this.damageEntityAt(e.x, e.y, dmgAmt, pMod); if(e.hp>0 && this.isValid(px+dx, py+dy) && !this.getEnemyAt(px+dx, py+dy)) {e.x=px+dx; e.y=py+dy;} }
            }
        }
        else if (card.id === 9 || card.id === 10) {
            let target = dmgCells[dmgCells.length-1];
            if (target && !this.getEnemyAt(target.x, target.y)) { this.player.x=target.x; this.player.y=target.y; }
        }

        card.cd = this.getMaxCd(card); this.hand.push(card);
        this.aimingIndex++; this.checkTraps(); this.setupNextAim();
    }

    damageEntityAt(x, y, amount, applyPoison = false) {
        if (this.player.x === x && this.player.y === y) {
            this.player.hp -= amount; 
            if (this.player.hp <= 0) this.gameOver();
        } else {
            let e = this.getEnemyAt(x, y);
            if (e) {
                e.hp -= amount;
                if (applyPoison && e.hp > 0) e.poison = 3;
                if (e.hp <= 0) { 
                    this.player.coins += e.coins; 
                    this.enemies = this.enemies.filter(en => en.id !== e.id); 
                }
            }
        }
    }
    checkTraps() {
        for(let i=this.traps.length-1; i>=0; i--) {
            let e = this.getEnemyAt(this.traps[i].x, this.traps[i].y);
            if (e) { this.damageEntityAt(e.x, e.y, 2); if (e.hp > 0) e.stunned = true; this.traps.splice(i, 1); }
        }
    }

    gameOver() { this.state = 'GAMEOVER'; document.getElementById('gameover-overlay').classList.remove('hidden'); document.getElementById('go-coins').innerText = this.player.coins; }
    victory() { this.state = 'VICTORY'; document.getElementById('victory-overlay').classList.remove('hidden'); document.getElementById('vic-coins').innerText = this.player.coins; }

    returnToMenuFromRun() {
        this.saveData.coins += this.player.coins;
        this.saveGame();
        
        ['gameover-overlay', 'victory-overlay', 'reward-draft-overlay', 'reward-upgrade-overlay', 'draft-overlay'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.classList.add('hidden');
        });
        this.openMenu();
    }

    // --- AI NEPŘÁTEL ---
    endTurn() {
        if (this.state === 'GAMEOVER' || this.state === 'VICTORY' || this.state === 'TRANSITION') return;
        this.enemies.forEach(e => { if (e.poison > 0) { this.damageEntityAt(e.x, e.y, 1); e.poison--; } });
        
        this.checkTraps();
        if (this.state !== 'GAMEOVER' && this.state !== 'VICTORY') this.processEnemiesTurn();
        if (this.state !== 'GAMEOVER' && this.state !== 'VICTORY') this.checkWaveClear();
        
        if (this.player.screamCd > 0) this.player.screamCd--;
        this.hand.forEach(c => { if (c.cd > 0) c.cd--; });
        
        if (this.state === 'PLAY') this.render();
    }

    processEnemiesTurn() {
        this.enemies.filter(e => e.state === 'AIMING' && !e.stunned).forEach(e => {
            if (e.type === 'boss') {
                if (e.bossPhase === 1) { 
                    if (e.bossPrep > 0) { e.bossPrep--; return; } 
                    let p = this.player;
                    if (Math.abs(p.x - e.x) <= 3 && Math.abs(p.y - e.y) <= 3) {
                        this.damageEntityAt(p.x, p.y, e.dmgAmt);
                        if (this.isValid(e.x + e.aimData.dx, e.y + e.aimData.dy)) {
                            p.x = e.x + e.aimData.dx; p.y = e.y + e.aimData.dy;
                            if(this.isValid(p.x + e.aimData.dx, p.y + e.aimData.dy) && !this.getEnemyAt(p.x + e.aimData.dx, p.y + e.aimData.dy)) {
                                p.x += e.aimData.dx; p.y += e.aimData.dy;
                            }
                        }
                    }
                } else {
                    let cells = this.getEnemyAttackCells(e); cells.forEach(c => this.damageEntityAt(c.x, c.y, e.dmgAmt));
                }
                if(e.type==='boss' && (e.bossPhase!==1 || e.bossPrep===0)) { e.state = 'READY'; e.aimData = null; e.bossPhase = (e.bossPhase + 1) % 3; }
            } else {
                let cells = this.getEnemyAttackCells(e); cells.forEach(c => this.damageEntityAt(c.x, c.y, e.dmgAmt)); e.state = 'READY'; e.aimData = null;
            }
        });

        this.enemies.filter(e => e.hp > 0).forEach(e => {
            if (e.stunned) { e.stunned = false; return; }
            if (e.state === 'EQUIPPING') { e.state = 'READY'; return; }
            if (e.state === 'READY') {
                const p = this.player; const dx = p.x - e.x, dy = p.y - e.y;
                const distO = Math.abs(dx) + Math.abs(dy); const distM = Math.max(Math.abs(dx), Math.abs(dy));

                if (e.type === 'boss') {
                    let bdx = Math.abs(dx) > Math.abs(dy) ? Math.sign(dx) : 0; let bdy = bdx===0 ? Math.sign(dy) : 0;
                    if (bdx===0 && bdy===0) { bdx=0; bdy=1; }
                    e.aimData = {dx: bdx, dy: bdy}; e.state = 'AIMING';
                    if (e.bossPhase === 0) { e.dmgAmt = 2; }
                    else if (e.bossPhase === 1) { e.dmgAmt = 2; e.bossPrep = 1; this.spawnEnemy(['pawn','archer','spearman'][Math.floor(Math.random()*3)], false); }
                    else if (e.bossPhase === 2) { e.dmgAmt = 3; }
                }
                else if (e.type === 'pawn') {
                    if (distO === 1) { e.state = 'AIMING'; e.aimData = {dx, dy}; e.dmgAmt = 2; } else this.moveAi(e, p.x, p.y, true);
                } 
                else if (e.type === 'archer') {
                    if (distM <= 2) {
                        if (!this.moveAi(e, p.x, p.y, false)) {
                            let fdx = Math.abs(dx)>Math.abs(dy)?Math.sign(dx):0; let fdy = fdx===0?Math.sign(dy):0;
                            e.state = 'AIMING'; e.aimData = {dx:fdx, dy:fdy}; e.dmgAmt = 2;
                        }
                    } else {
                        if (e.x === p.x || e.y === p.y) { e.state = 'AIMING'; e.aimData = {dx: Math.sign(dx), dy: Math.sign(dy)}; e.dmgAmt = 2; } else this.alignAi(e, p.x, p.y);
                    }
                }
                else if (e.type === 'spearman') {
                    if ((distO === 1 || distO === 2) && (e.x === p.x || e.y === p.y)) { e.state = 'AIMING'; e.aimData = {dx: Math.sign(dx), dy: Math.sign(dy)}; e.dmgAmt = 2; } else this.moveAi(e, p.x, p.y, true);
                }
                else if (e.type === 'shaman') {
                    if (distM <= 2) { if (!this.moveAi(e, p.x, p.y, false)) { e.state = 'AIMING'; e.aimData = {dx, dy}; e.dmgAmt = 1; } } else { e.state = 'AIMING'; e.aimData = {dx, dy}; e.dmgAmt = 1; }
                }
            }
        });
    }

    moveAi(enemy, tx, ty, towards) {
        let moves = [ {dx:0, dy:-1}, {dx:0, dy:1}, {dx:-1, dy:0}, {dx:1, dy:0} ];
        let valid = moves.map(m => ({x: enemy.x+m.dx, y: enemy.y+m.dy})).filter(c => this.isValid(c.x, c.y) && !this.getEnemyAt(c.x, c.y) && !(c.x===this.player.x && c.y===this.player.y));
        if (valid.length === 0) return false;
        valid.sort((a, b) => { let dA = Math.abs(a.x - tx) + Math.abs(a.y - ty), dB = Math.abs(b.x - tx) + Math.abs(b.y - ty); return towards ? dA - dB : dB - dA; });
        if (!towards && (Math.abs(valid[0].x - tx) + Math.abs(valid[0].y - ty)) <= (Math.abs(enemy.x - tx) + Math.abs(enemy.y - ty))) return false;
        enemy.x = valid[0].x; enemy.y = valid[0].y; return true;
    }
    alignAi(enemy, tx, ty) {
        let moves = [ {dx:0, dy:-1}, {dx:0, dy:1}, {dx:-1, dy:0}, {dx:1, dy:0} ];
        let valid = moves.map(m => ({x: enemy.x+m.dx, y: enemy.y+m.dy})).filter(c => this.isValid(c.x, c.y) && !this.getEnemyAt(c.x, c.y) && !(c.x===this.player.x && c.y===this.player.y));
        let aligned = valid.find(c => c.x === tx || c.y === ty);
        if (aligned) { enemy.x = aligned.x; enemy.y = aligned.y; return true; }
        return this.moveAi(enemy, tx, ty, true);
    }

    getEnemyAttackCells(e) {
        if (!e.aimData || e.state !== 'AIMING') return [];
        let cells = []; let cx = e.x + e.aimData.dx, cy = e.y + e.aimData.dy;
        
        if (e.type === 'pawn') { if(this.isValid(cx, cy)) cells.push({x:cx, y:cy}); }
        else if (e.type === 'spearman') { if(this.isValid(cx, cy)) cells.push({x:cx, y:cy}); if(this.isValid(cx+e.aimData.dx, cy+e.aimData.dy)) cells.push({x: cx+e.aimData.dx, y: cy+e.aimData.dy}); }
        else if (e.type === 'archer' || (e.type === 'boss' && e.bossPhase === 0)) {
            for(let i=1; i<=5; i++) {
                let nx = e.x + e.aimData.dx*i, ny = e.y + e.aimData.dy*i;
                if (!this.isValid(nx, ny)) break; cells.push({x:nx, y:ny});
                if (this.getEnemyAt(nx, ny) || (nx === this.player.x && ny === this.player.y)) break;
            }
        }
        else if (e.type === 'shaman') { [{dx:0,dy:0}, {dx:-1,dy:-1}, {dx:1,dy:-1}, {dx:-1,dy:1}, {dx:1,dy:1}].forEach(m => { if(this.isValid(cx+m.dx, cy+m.dy)) cells.push({x: cx+m.dx, y: cy+m.dy}); }); }
        else if (e.type === 'boss' && e.bossPhase === 1) {
            for(let i=1; i<=5; i++) { let nx = e.x + e.aimData.dx*i, ny = e.y + e.aimData.dy*i; if (!this.isValid(nx, ny)) break; cells.push({x:nx, y:ny}); }
        }
        else if (e.type === 'boss' && e.bossPhase === 2) {
            let ortho = [{x:cx, y:cy}, {x:cx+e.aimData.dx, y:cy+e.aimData.dy}];
            let perp = e.aimData.dx===0 ? {dx:1, dy:0} : {dx:0, dy:1};
            let left = {x:cx+perp.dx, y:cy+perp.dy}, right = {x:cx-perp.dx, y:cy-perp.dy};
            let dl = {x:cx+e.aimData.dx+perp.dx, y:cy+e.aimData.dy+perp.dy}, dr = {x:cx+e.aimData.dx-perp.dx, y:cy+e.aimData.dy-perp.dy};
            [...ortho, left, right, dl, dr].forEach(c => { if(this.isValid(c.x, c.y)) cells.push(c); });
        }
        return cells;
    }

    // --- VYKRESLOVÁNÍ ---
    createCardElement(card, context, index) {
        const div = document.createElement('div');
        div.className = `card type-${card.type} ${card.cd > 0 ? 'cooldown' : ''}`;
        if (context === 'prepared' && index === this.aimingIndex && this.state === 'AIMING') div.classList.add('active-aim');
        if (context === 'draft') div.onclick = () => this.toggleDraftCard(index);
        else if (context === 'reward-draft') div.onclick = () => this.selectRewardDraftCard(index);
        else if (context === 'reward-upgrade') div.onclick = () => this.applyUpgradeToCard(card.uid);
        else if (context === 'hand') div.onclick = () => this.equipCard(card.uid);
        
        let modsHtml = card.mods.length > 0 ? `<div class="card-mods">${card.mods.map(m=>m.text).join(', ')}</div>` : '';
        div.innerHTML = `${card.cd > 0 ? `<div class="card-cd-overlay">${card.cd}</div>` : ''}
            <div class="card-header">${card.type}</div><div class="card-name">${card.name}</div>
            <div class="card-stats"><span>🗡️ ${this.getDmg(card)}</span><span>⏳ ${this.getMaxCd(card)}</span></div>
            <div class="card-desc">${card.desc}</div>${modsHtml}`;
        return div;
    }

    renderTopBar() {
        document.getElementById('ui-hp').innerText = `${this.player.hp}/${this.player.maxHp}`;
        document.getElementById('ui-coins').innerText = this.player.coins;
        document.getElementById('ui-wave').innerText = this.wave;
    }

    renderGridOnly() {
        const grid = document.getElementById('grid'); grid.innerHTML = '';
        let allEnemyTelegraphs = [];
        this.enemies.forEach(e => { allEnemyTelegraphs.push(...this.getEnemyAttackCells(e)); });

        for (let y = 0; y < this.height; y++) {
            for (let x = 0; x < this.width; x++) {
                const cell = document.createElement('div'); cell.className = 'cell';
                if (allEnemyTelegraphs.some(c => c.x === x && c.y === y)) cell.classList.add('telegraph');

                if (this.state === 'AIMING') {
                    cell.onmouseenter = () => this.handleCellHover(x, y);
                    if (this.aimTargetCells.some(c => c.x === x && c.y === y)) {
                        cell.classList.add(['Movement', 'Special'].includes(this.prepared[this.aimingIndex]?.type) ? 'target-preview-move' : 'target-preview');
                    }
                } else if (this.state === 'PLAY' && Math.abs(x - this.player.x) + Math.abs(y - this.player.y) === 1) cell.classList.add('adjacent');

                cell.onclick = () => this.handleCellClick(x, y);

                let content = '';
                if (this.traps.some(t => t.x === x && t.y === y)) content += `<div class="object-layer">${SVGs.trap}</div>`;

                if (this.player.x === x && this.player.y === y) {
                    content += `<div class="entity player"><div class="status-icon">⚔️</div>${SVGs.viking}<div class="name">${this.player.name}</div><div class="hp-bar"><div class="hp-fill" style="width: ${(this.player.hp/this.player.maxHp)*100}%"></div></div></div>`;
                } else {
                    const e = this.getEnemyAt(x, y);
                    if (e) {
                        let status = e.stunned ? '💫' : (e.state==='EQUIPPING' ? '⚙️' : (e.state==='AIMING' ? '🎯' : '⚔️'));
                        let pIcon = e.poison > 0 ? `<span class="poison-icon">🟢</span>` : '';
                        content += `<div class="entity enemy"><div class="status-icon">${status}${pIcon}</div>${e.svg}<div class="name">${e.name}</div><div class="hp-bar ${e.type==='boss'?'boss-hp':''}"><div class="hp-fill" style="width: ${(e.hp/e.maxHp)*100}%"></div></div></div>`;
                    }
                }
                cell.innerHTML = content; grid.appendChild(cell);
            }
        }
    }

    render() {
        this.renderTopBar();
        const screamBtn = document.getElementById('btn-scream');
        if (this.player.screamCd > 0) { screamBtn.disabled = true; document.getElementById('ui-cooldown').innerText = `(${this.player.screamCd})`; } 
        else { screamBtn.disabled = false; document.getElementById('ui-cooldown').innerText = ""; }

        document.getElementById('btn-execute').disabled = this.prepared.length === 0 || this.state !== 'PLAY';

        const handCont = document.getElementById('hand-cards'); handCont.innerHTML = '';
        this.hand.forEach(c => handCont.appendChild(this.createCardElement(c, 'hand')));

        const prepCont = document.getElementById('prepared-slots'); prepCont.innerHTML = '';
        for(let i=0; i<this.maxPrepared; i++) {
            if (this.prepared[i]) prepCont.appendChild(this.createCardElement(this.prepared[i], 'prepared', i));
            else { let em = document.createElement('div'); em.className='card-slot'; em.innerHTML=`<div style="margin:auto;color:#555;">Slot ${i+1}</div>`; prepCont.appendChild(em); }
        }
        this.renderGridOnly();
    }
}
let game; window.onload = () => { game = new Game(); };