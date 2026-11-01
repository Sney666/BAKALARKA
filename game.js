const SVGs = {
    viking: `<svg viewBox="0 0 100 100" class="avatar"><path d="M15,95 Q50,60 85,95" stroke="#b8860b" stroke-width="4" fill="#4682b4" /><path d="M30,50 L50,85 L70,50 Z" fill="#d2691e" /><path d="M25,50 C25,20 75,20 75,50 Z" fill="#4682b4" stroke="#b8860b" stroke-width="3"/><circle cx="40" cy="55" r="4" fill="#fff"/><circle cx="60" cy="55" r="4" fill="#fff"/><circle cx="40" cy="55" r="1.5" fill="#000"/><circle cx="60" cy="55" r="1.5" fill="#000"/><path d="M25,45 Q5,30 10,10 Q25,25 35,35" fill="#daa520" stroke="#000" stroke-width="1"/><path d="M75,45 Q95,30 90,10 Q75,25 65,35" fill="#daa520" stroke="#000" stroke-width="1"/></svg>`,
    orc: `<svg viewBox="0 0 100 100" class="avatar"><path d="M20,95 Q50,70 80,95" stroke="#333" stroke-width="4" fill="#4a5d23" /><circle cx="50" cy="45" r="22" fill="#556b2f" stroke="#222" stroke-width="2"/><polygon points="35,40 45,45 35,48" fill="red"/><polygon points="65,40 55,45 65,48" fill="red"/><path d="M45,58 L50,53 L55,58" stroke="#fff" stroke-width="2" fill="none"/></svg>`,
    trap: `<svg viewBox="0 0 100 100" class="avatar"><circle cx="50" cy="50" r="40" fill="none" stroke="#555" stroke-width="4"/><path d="M50,10 L40,30 L60,30 Z M50,90 L40,70 L60,70 Z M10,50 L30,40 L30,60 Z M90,50 L70,40 L70,60 Z" fill="#777"/></svg>`
};

// Všechny karty ve hře
const CARD_DB = [
    { id: 1, name: "Viking Axe", type: "Melee", dmg: 3, maxCd: 4, unlocked: true, aim: 'ortho',
      desc: "Zásah 1 sousedního pole (ortogonálně)." },
    { id: 2, name: "Wooden Club", type: "Melee", dmg: 2, maxCd: 5, unlocked: false, aim: 'ortho',
      desc: "Zásah a odhození nepřítele o 1 pole." },
    { id: 3, name: "Two-handed Hammer", type: "Melee", dmg: 4, maxCd: 6, unlocked: false, aim: 'ortho',
      desc: "Zásah 3 polí v oblouku před hráčem." },
    { id: 4, name: "Throwing Axe", type: "Ranged", dmg: 2, maxCd: 3, unlocked: true, aim: 'ortho',
      desc: "Projektil rovně, zasáhne prvního v cestě." },
    { id: 5, name: "Hunting Bow", type: "Ranged", dmg: 3, maxCd: 5, unlocked: false, aim: 'diag',
      desc: "Projektil diagonálně, zasáhne prvního." },
    { id: 6, name: "Heavy Spear", type: "Ranged", dmg: 4, maxCd: 6, unlocked: false, aim: 'ortho',
      desc: "Průrazný projektil, zasáhne 2 nepřátele v řadě." },
    { id: 7, name: "Bear Trap", type: "Special", dmg: 2, maxCd: 6, unlocked: true, aim: 'self',
      desc: "Položí past na tvé pole. Zraní a zastaví nepřítele." },
    { id: 8, name: "Boat Hook", type: "Special", dmg: 1, maxCd: 5, unlocked: false, aim: 'ortho',
      desc: "Vystřelí hák (max 3 pole), přitáhne cíl k sobě." },
    { id: 9, name: "Diagonal Dash", type: "Movement", dmg: 0, maxCd: 4, unlocked: true, aim: 'diag',
      desc: "Přesun hráče o 1 pole diagonálně." },
    { id: 10, name: "Slide", type: "Movement", dmg: 0, maxCd: 5, unlocked: false, aim: 'ortho',
      desc: "Přesun o 2 pole rovně. Zastaví se o nepřítele." }
];

class Game {
    constructor() {
        this.width = 5; this.height = 5; this.wave = 1;
        this.state = 'DRAFT'; // DRAFT, PLAY, AIMING
        
        this.player = { x: 2, y: 2, hp: 5, maxHp: 5, coins: 0, name: "Viking", screamCd: 0, maxScreamCd: 6 };
        this.enemies = [
            { id: 1, x: 1, y: 1, hp: 3, maxHp: 3, name: "Orc", stunned: false },
            { id: 2, x: 3, y: 1, hp: 3, maxHp: 3, name: "Orc", stunned: false }
        ];
        this.traps = []; // {x, y}

        // Karetní systém
        this.deck = [];
        this.hand = [];
        this.prepared = [];
        this.maxPrepared = 4;
        
        this.draftSelection = [];
        this.aimingIndex = 0;
        this.aimTargetCells = [];
        this.hoverX = -1; this.hoverY = -1;

        this.init();
    }

    init() {
        this.bindInput();
        this.startDraft();
    }

    // --- DRAFT KARET ---
    startDraft() {
        this.state = 'DRAFT';
        const unlocked = CARD_DB.filter(c => c.unlocked);
        // Zamíchání a výběr 4 karet
        this.deck = unlocked.sort(() => 0.5 - Math.random()).slice(0, 4).map(c => ({...c, cd: 0, uid: Math.random()}));
        
        const container = document.getElementById('draft-cards');
        container.innerHTML = '';
        this.deck.forEach((card, index) => {
            const el = this.createCardElement(card, 'draft', index);
            container.appendChild(el);
        });
        document.getElementById('draft-overlay').classList.remove('hidden');
    }

    toggleDraftCard(index) {
        const idx = this.draftSelection.indexOf(index);
        if (idx > -1) {
            this.draftSelection.splice(idx, 1);
        } else {
            if (this.draftSelection.length < 2) this.draftSelection.push(index);
        }
        
        // Update UI
        Array.from(document.getElementById('draft-cards').children).forEach((el, i) => {
            el.classList.toggle('selected-draft', this.draftSelection.includes(i));
        });
        document.getElementById('btn-confirm-draft').disabled = this.draftSelection.length !== 2;
    }

    confirmDraft() {
        this.draftSelection.forEach(idx => {
            this.hand.push({...this.deck[idx]});
        });
        document.getElementById('draft-overlay').classList.add('hidden');
        this.state = 'PLAY';
        this.render();
    }

    // --- ZPRACOVÁNÍ VSTUPU ---
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
        if (this.state !== 'AIMING') return;
        if (this.hoverX !== x || this.hoverY !== y) {
            this.hoverX = x; this.hoverY = y;
            this.calculateAimPreview();
            this.renderGridOnly();
        }
    }

    handleCellClick(x, y) {
        if (this.state === 'PLAY') {
            const dx = x - this.player.x; const dy = y - this.player.y;
            if (Math.abs(dx) + Math.abs(dy) === 1) this.tryMovePlayer(dx, dy);
        } else if (this.state === 'AIMING') {
            // Pokud klikne na zvýrazněné políčko (nebo na sebe u pasti)
            if (this.aimTargetCells.some(c => c.x === x && c.y === y) || (this.hand[0]?.aim === 'self' && x === this.player.x && y === this.player.y)) {
                this.executeAim(x, y);
            }
        }
    }

    // --- LOGIKA AKCÍ (PLAY) ---
    tryMovePlayer(dx, dy) {
        const nx = this.player.x + dx; const ny = this.player.y + dy;
        if (nx < 0 || nx >= this.width || ny < 0 || ny >= this.height) return;
        if (this.getEnemyAt(nx, ny)) return;
        this.player.x = nx; this.player.y = ny;
        this.endTurn();
    }

    wait() { if (this.state === 'PLAY') this.endTurn(); }

    vikingScream() {
        if (this.state !== 'PLAY' || this.player.screamCd > 0) return;
        this.enemies.forEach(e => {
            const dx = e.x - this.player.x; const dy = e.y - this.player.y;
            if (Math.abs(dx) <= 1 && Math.abs(dy) <= 1) {
                const nx = e.x + Math.sign(dx); const ny = e.y + Math.sign(dy);
                if (nx >= 0 && nx < this.width && ny >= 0 && ny < this.height && !this.getEnemyAt(nx, ny)) {
                    e.x = nx; e.y = ny;
                }
            }
        });
        this.player.screamCd = this.player.maxScreamCd;
        this.endTurn();
    }

    // --- KARETNÍ MECHANIKY ---
    equipCard(uid) {
        if (this.state !== 'PLAY' || this.prepared.length >= this.maxPrepared) return;
        const cardIndex = this.hand.findIndex(c => c.uid === uid);
        if (cardIndex === -1 || this.hand[cardIndex].cd > 0) return;
        
        this.prepared.push(this.hand.splice(cardIndex, 1)[0]);
        this.endTurn();
    }

    startExecution() {
        if (this.state !== 'PLAY' || this.prepared.length === 0) return;
        this.state = 'AIMING';
        this.aimingIndex = 0;
        this.setupNextAim();
    }

    setupNextAim() {
        if (this.aimingIndex >= this.prepared.length) {
            this.state = 'PLAY';
            this.prepared = [];
            this.endTurn();
            return;
        }
        
        const card = this.prepared[this.aimingIndex];
        this.aimTargetCells = [];
        
        if (card.aim === 'self') {
            this.aimTargetCells = [{x: this.player.x, y: this.player.y}];
            this.render();
        } else {
            this.render(); // Zvýrazní aktuální kartu
        }
    }

    getDirection(hx, hy, aimType) {
        let dx = hx - this.player.x;
        let dy = hy - this.player.y;
        if (dx === 0 && dy === 0) return null;

        if (aimType === 'ortho') {
            if (Math.abs(dx) > Math.abs(dy)) { dy = 0; dx = Math.sign(dx); } 
            else { dx = 0; dy = Math.sign(dy); }
        } else if (aimType === 'diag') {
            dx = dx === 0 ? 1 : Math.sign(dx);
            dy = dy === 0 ? 1 : Math.sign(dy);
        }
        return { dx, dy };
    }

    calculateAimPreview() {
        this.aimTargetCells = [];
        const card = this.prepared[this.aimingIndex];
        if (!card) return;

        if (card.aim === 'self') {
            this.aimTargetCells = [{x: this.player.x, y: this.player.y}];
            return;
        }

        const dir = this.getDirection(this.hoverX, this.hoverY, card.aim);
        if (!dir) return;
        const px = this.player.x, py = this.player.y;
        const dx = dir.dx, dy = dir.dy;

        switch (card.id) {
            case 1: case 2: // Axe, Club
                this.addAimCell(px + dx, py + dy); break;
            case 3: // Hammer (Arc)
                this.addAimCell(px + dx, py + dy);
                if (dx !== 0) { this.addAimCell(px + dx, py - 1); this.addAimCell(px + dx, py + 1); }
                else { this.addAimCell(px - 1, py + dy); this.addAimCell(px + 1, py + dy); }
                break;
            case 4: case 5: // Throwing Axe, Bow
                for (let i = 1; i <= 5; i++) {
                    const nx = px + dx*i, ny = py + dy*i;
                    if (!this.addAimCell(nx, ny)) break; // Zdi
                    if (this.getEnemyAt(nx, ny)) break; // Zastaví se
                }
                break;
            case 6: // Spear (Piercing)
                let hits = 0;
                for (let i = 1; i <= 5; i++) {
                    const nx = px + dx*i, ny = py + dy*i;
                    if (!this.addAimCell(nx, ny)) break;
                    if (this.getEnemyAt(nx, ny)) hits++;
                    if (hits >= 2) break;
                }
                break;
            case 8: // Harpoon
                for (let i = 1; i <= 3; i++) {
                    const nx = px + dx*i, ny = py + dy*i;
                    if (!this.addAimCell(nx, ny)) break;
                    if (this.getEnemyAt(nx, ny)) break;
                }
                break;
            case 9: // Diag Dash
                this.addAimCell(px + dx, py + dy); break;
            case 10: // Slide
                for(let i = 1; i <= 2; i++) {
                    const nx = px + dx*i, ny = py + dy*i;
                    if (this.isValid(nx, ny) && !this.getEnemyAt(nx, ny)) this.addAimCell(nx, ny);
                    else break;
                }
                break;
        }
    }

    addAimCell(x, y) {
        if (this.isValid(x, y)) {
            this.aimTargetCells.push({x, y});
            return true;
        }
        return false;
    }

    isValid(x, y) { return x >= 0 && x < this.width && y >= 0 && y < this.height; }

    executeAim(tx, ty) {
        const card = this.prepared[this.aimingIndex];
        const dir = this.getDirection(tx, ty, card.aim) || {dx: 0, dy: 0};
        const dx = dir.dx, dy = dir.dy;
        const px = this.player.x, py = this.player.y;

        const dmgCells = [...this.aimTargetCells]; // kopie

        // Aplikace efektů podle ID karty
        if ([1, 3, 4, 5, 6].includes(card.id)) {
            // Jen damage do označených
            dmgCells.forEach(c => {
                const e = this.getEnemyAt(c.x, c.y);
                if (e) this.damageEnemy(e, card.dmg);
            });
        } 
        else if (card.id === 2) { // Club - dmg a knockback
            const e = this.getEnemyAt(px + dx, py + dy);
            if (e) {
                this.damageEnemy(e, card.dmg);
                const nx = e.x + dx, ny = e.y + dy;
                if (this.isValid(nx, ny) && !this.getEnemyAt(nx, ny)) { e.x = nx; e.y = ny; }
            }
        }
        else if (card.id === 7) { // Trap
            if (!this.traps.some(t => t.x === px && t.y === py)) {
                this.traps.push({x: px, y: py});
            }
        }
        else if (card.id === 8) { // Harpoon
            const target = dmgCells[dmgCells.length - 1]; // Poslední bod (tam se zastavila střela)
            if (target) {
                const e = this.getEnemyAt(target.x, target.y);
                if (e) {
                    this.damageEnemy(e, card.dmg);
                    const pullX = px + dx, pullY = py + dy; // Sousední pole
                    if (this.isValid(pullX, pullY) && !this.getEnemyAt(pullX, pullY)) { e.x = pullX; e.y = pullY; }
                }
            }
        }
        else if (card.id === 9 || card.id === 10) { // Move
            const target = dmgCells[dmgCells.length - 1];
            if (target && !this.getEnemyAt(target.x, target.y)) {
                this.player.x = target.x; this.player.y = target.y;
            }
        }

        // Nastav CD a přesuň zpět do ruky
        card.cd = card.maxCd;
        this.hand.push(card);
        
        // Další karta
        this.aimingIndex++;
        this.checkTraps();
        this.setupNextAim();
    }

    damageEnemy(enemy, amount) {
        enemy.hp -= amount;
        if (enemy.hp <= 0) {
            this.enemies = this.enemies.filter(e => e.id !== enemy.id);
        }
    }

    checkTraps() {
        for (let i = this.traps.length - 1; i >= 0; i--) {
            const t = this.traps[i];
            const e = this.getEnemyAt(t.x, t.y);
            if (e) {
                this.damageEnemy(e, 2);
                if (e.hp > 0) e.stunned = true;
                this.traps.splice(i, 1); // Odstranit past po spuštění
            }
        }
    }

    // --- TAHY A RENDER ---
    endTurn() {
        this.checkTraps();

        // Enemy turn (Zatím jen řeší stun)
        this.enemies.forEach(e => {
            if (e.stunned) e.stunned = false; // Příště už může
        });

        // Cooldowns
        if (this.player.screamCd > 0) this.player.screamCd--;
        this.hand.forEach(c => { if (c.cd > 0) c.cd--; });
        
        this.render();
    }

    getEnemyAt(x, y) { return this.enemies.find(e => e.x === x && e.y === y); }

    // --- VYKRESLOVÁNÍ ---
    createCardElement(card, context, index) {
        const div = document.createElement('div');
        div.className = `card type-${card.type} ${card.cd > 0 ? 'cooldown' : ''}`;
        if (context === 'prepared' && index === this.aimingIndex && this.state === 'AIMING') div.classList.add('active-aim');
        
        if (context === 'draft') div.onclick = () => this.toggleDraftCard(index);
        else if (context === 'hand') div.onclick = () => this.equipCard(card.uid);

        div.innerHTML = `
            ${card.cd > 0 ? `<div class="card-cd-overlay">${card.cd}</div>` : ''}
            <div class="card-header">${card.type}</div>
            <div class="card-name">${card.name}</div>
            <div class="card-stats"><span>🗡️ ${card.dmg}</span><span>⏳ ${card.maxCd}</span></div>
            <div class="card-desc">${card.desc}</div>
        `;
        return div;
    }

    renderGridOnly() {
        const grid = document.getElementById('grid');
        grid.innerHTML = '';
        for (let y = 0; y < this.height; y++) {
            for (let x = 0; x < this.width; x++) {
                const cell = document.createElement('div');
                cell.className = 'cell';
                
                // Míření (Hover preview)
                if (this.state === 'AIMING') {
                    cell.onmouseenter = () => this.handleCellHover(x, y);
                    if (this.aimTargetCells.some(c => c.x === x && c.y === y)) {
                        const isMove = ['Movement', 'Special'].includes(this.prepared[this.aimingIndex]?.type);
                        cell.classList.add(isMove ? 'target-preview-move' : 'target-preview');
                    }
                } 
                // Zvýraznění klasického pohybu
                else if (this.state === 'PLAY') {
                    if (Math.abs(x - this.player.x) + Math.abs(y - this.player.y) === 1) cell.classList.add('adjacent');
                }

                cell.onclick = () => this.handleCellClick(x, y);

                // Kreslení entit a pastí
                let content = '';
                if (this.traps.some(t => t.x === x && t.y === y)) {
                    content += `<div class="object-layer">${SVGs.trap}</div>`;
                }

                if (this.player.x === x && this.player.y === y) {
                    content += `<div class="entity player"><div class="status-icon">⚔️</div>${SVGs.viking}<div class="name">${this.player.name}</div>
                                <div class="hp-bar"><div class="hp-fill" style="width: ${(this.player.hp/this.player.maxHp)*100}%"></div></div></div>`;
                } else {
                    const enemy = this.getEnemyAt(x, y);
                    if (enemy) {
                        content += `<div class="entity enemy"><div class="status-icon">${enemy.stunned ? '💫' : '🩸'}</div>${SVGs.orc}<div class="name">${enemy.name}</div>
                                    <div class="hp-bar"><div class="hp-fill" style="width: ${(enemy.hp/enemy.maxHp)*100}%"></div></div></div>`;
                    }
                }
                cell.innerHTML = content;
                grid.appendChild(cell);
            }
        }
    }

    render() {
        document.getElementById('ui-hp').innerText = `${this.player.hp}/${this.player.maxHp}`;
        document.getElementById('ui-coins').innerText = this.player.coins;
        document.getElementById('ui-wave').innerText = this.wave;

        const screamBtn = document.getElementById('btn-scream');
        if (this.player.screamCd > 0) { screamBtn.disabled = true; document.getElementById('ui-cooldown').innerText = `(${this.player.screamCd})`; } 
        else { screamBtn.disabled = false; document.getElementById('ui-cooldown').innerText = ""; }

        const execBtn = document.getElementById('btn-execute');
        execBtn.disabled = this.prepared.length === 0 || this.state !== 'PLAY';

        // Render Hand
        const handCont = document.getElementById('hand-cards');
        handCont.innerHTML = '';
        this.hand.forEach(c => handCont.appendChild(this.createCardElement(c, 'hand')));

        // Render Prepared
        const prepCont = document.getElementById('prepared-slots');
        prepCont.innerHTML = '';
        for(let i=0; i<this.maxPrepared; i++) {
            if (this.prepared[i]) prepCont.appendChild(this.createCardElement(this.prepared[i], 'prepared', i));
            else {
                const empty = document.createElement('div'); empty.className = 'card-slot';
                empty.innerHTML = `<div style="margin:auto;color:#555;">Slot ${i+1}</div>`;
                prepCont.appendChild(empty);
            }
        }

        this.renderGridOnly();
    }
}

let game;
window.onload = () => { game = new Game(); };