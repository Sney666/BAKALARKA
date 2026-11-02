const SVGs = {
    viking: `<svg viewBox="0 0 100 100" class="avatar"><path d="M15,95 Q50,60 85,95" stroke="#b8860b" stroke-width="4" fill="#4682b4" /><path d="M30,50 L50,85 L70,50 Z" fill="#d2691e" /><path d="M25,50 C25,20 75,20 75,50 Z" fill="#4682b4" stroke="#b8860b" stroke-width="3"/><circle cx="40" cy="55" r="4" fill="#fff"/><circle cx="60" cy="55" r="4" fill="#fff"/><circle cx="40" cy="55" r="1.5" fill="#000"/><circle cx="60" cy="55" r="1.5" fill="#000"/><path d="M25,45 Q5,30 10,10 Q25,25 35,35" fill="#daa520" stroke="#000" stroke-width="1"/><path d="M75,45 Q95,30 90,10 Q75,25 65,35" fill="#daa520" stroke="#000" stroke-width="1"/></svg>`,
    pawn: `<svg viewBox="0 0 100 100" class="avatar"><path d="M20,95 Q50,65 80,95" stroke="#444" stroke-width="4" fill="#777" /><circle cx="50" cy="40" r="20" fill="#999" stroke="#333" stroke-width="2"/><circle cx="35" cy="70" r="15" fill="#654321" stroke="#ccc" stroke-width="2"/><path d="M65,50 L90,20 L80,10 Z" fill="#aaa" stroke="#333" stroke-width="1"/></svg>`,
    archer: `<svg viewBox="0 0 100 100" class="avatar"><path d="M25,95 Q50,60 75,95" stroke="#222" stroke-width="3" fill="#2e8b57" /><path d="M50,15 Q30,30 30,50 Q30,65 50,75 Q70,65 70,50 Q70,30 50,15 Z" fill="#228b22" stroke="#111" stroke-width="2"/><circle cx="42" cy="45" r="3" fill="#fff"/><circle cx="58" cy="45" r="3" fill="#fff"/><path d="M70,30 Q90,50 70,80" fill="none" stroke="#8b4513" stroke-width="4"/><line x1="70" y1="30" x2="70" y2="80" stroke="#fff" stroke-width="1"/></svg>`,
    spearman: `<svg viewBox="0 0 100 100" class="avatar"><path d="M15,95 Q50,70 85,95" stroke="#333" stroke-width="4" fill="#696969" /><rect x="35" y="25" width="30" height="35" rx="5" fill="#a9a9a9" stroke="#444" stroke-width="2"/><rect x="40" y="30" width="20" height="10" fill="#222"/><line x1="80" y1="95" x2="80" y2="10" stroke="#8b4513" stroke-width="4"/><polygon points="75,10 85,10 80,0" fill="#ccc" stroke="#555" stroke-width="1"/></svg>`,
    shaman: `<svg viewBox="0 0 100 100" class="avatar"><path d="M20,95 Q50,50 80,95" stroke="#222" stroke-width="3" fill="#4b0082" /><rect x="35" y="25" width="30" height="35" rx="8" fill="#8b4513" stroke="#333" stroke-width="2"/><circle cx="42" cy="40" r="4" fill="#00ff00"/><circle cx="58" cy="40" r="4" fill="#00ff00"/><path d="M25,60 L20,75 L30,75 Z" fill="#00fa9a" stroke="#000" stroke-width="1"/></svg>`,
    trap: `<svg viewBox="0 0 100 100" class="avatar"><circle cx="50" cy="50" r="40" fill="none" stroke="#555" stroke-width="4"/><path d="M50,10 L40,30 L60,30 Z M50,90 L40,70 L60,70 Z M10,50 L30,40 L30,60 Z M90,50 L70,40 L70,60 Z" fill="#777"/></svg>`
};

const CARD_DB = [
    { id: 1, name: "Viking Axe", type: "Melee", dmg: 3, maxCd: 4, unlocked: true, aim: 'ortho', desc: "Zásah 1 sousedního pole (ortogonálně)." },
    { id: 2, name: "Wooden Club", type: "Melee", dmg: 2, maxCd: 5, unlocked: false, aim: 'ortho', desc: "Zásah a odhození o 1 pole." },
    { id: 3, name: "Two-handed Hammer", type: "Melee", dmg: 4, maxCd: 6, unlocked: false, aim: 'ortho', desc: "Zásah 3 polí v oblouku." },
    { id: 4, name: "Throwing Axe", type: "Ranged", dmg: 2, maxCd: 3, unlocked: true, aim: 'ortho', desc: "Projektil rovně, zasáhne prvního." },
    { id: 5, name: "Hunting Bow", type: "Ranged", dmg: 3, maxCd: 5, unlocked: false, aim: 'diag', desc: "Projektil diagonálně, zasáhne prvního." },
    { id: 6, name: "Heavy Spear", type: "Ranged", dmg: 4, maxCd: 6, unlocked: false, aim: 'ortho', desc: "Projme 2 nepřátele v řadě." },
    { id: 7, name: "Bear Trap", type: "Special", dmg: 2, maxCd: 6, unlocked: true, aim: 'self', desc: "Položí past na tvé pole." },
    { id: 8, name: "Boat Hook", type: "Special", dmg: 1, maxCd: 5, unlocked: false, aim: 'ortho', desc: "Přitáhne cíl na sousední pole." },
    { id: 9, name: "Diagonal Dash", type: "Movement", dmg: 0, maxCd: 4, unlocked: true, aim: 'diag', desc: "Přesun 1 pole diagonálně." },
    { id: 10, name: "Slide", type: "Movement", dmg: 0, maxCd: 5, unlocked: false, aim: 'ortho', desc: "Přesun 2 pole rovně." }
];

class Game {
    constructor() {
        this.width = 5; this.height = 5; this.wave = 1;
        this.state = 'DRAFT'; // DRAFT, PLAY, AIMING, GAMEOVER
        
        this.player = { x: 2, y: 2, hp: 5, maxHp: 5, coins: 0, name: "Viking", screamCd: 0, maxScreamCd: 6 };
        this.enemies = [];
        this.enemyIdCounter = 1;
        this.traps = [];
        
        this.deck = []; this.hand = []; this.prepared = []; this.maxPrepared = 4;
        this.draftSelection = []; this.aimingIndex = 0; this.aimTargetCells = [];
        this.hoverX = -1; this.hoverY = -1;

        this.init();
    }

    init() {
        this.bindInput();
        this.startDraft();
    }

    // --- SETUP NEPŘÁTEL ---
    spawnTestEnemies() {
        this.enemies = [];
        this.spawnEnemy('pawn', false);
        this.spawnEnemy('archer', true);
        this.spawnEnemy('spearman', false);
        this.spawnEnemy('shaman', true);
    }

    spawnEnemy(type, edgeOnly) {
        let cells = [];
        for(let y=0; y<this.height; y++) {
            for(let x=0; x<this.width; x++) {
                if (this.player.x === x && this.player.y === y) continue;
                if (this.getEnemyAt(x, y)) continue;
                if (edgeOnly && x!==0 && x!==this.width-1 && y!==0 && y!==this.height-1) continue;
                cells.push({x, y});
            }
        }
        if (cells.length === 0) return;
        const pos = cells[Math.floor(Math.random() * cells.length)];
        
        let enemy = { id: this.enemyIdCounter++, x: pos.x, y: pos.y, type: type, state: 'EQUIPPING', stunned: false, aimData: null };
        if (type === 'pawn') { enemy.hp = 3; enemy.maxHp = 3; enemy.coins = 2; enemy.name = 'Pawn'; enemy.svg = SVGs.pawn; }
        else if (type === 'archer') { enemy.hp = 2; enemy.maxHp = 2; enemy.coins = 3; enemy.name = 'Archer'; enemy.svg = SVGs.archer; }
        else if (type === 'spearman') { enemy.hp = 4; enemy.maxHp = 4; enemy.coins = 3; enemy.name = 'Spearman'; enemy.svg = SVGs.spearman; }
        else if (type === 'shaman') { enemy.hp = 3; enemy.maxHp = 3; enemy.coins = 4; enemy.name = 'Shaman'; enemy.svg = SVGs.shaman; }
        this.enemies.push(enemy);
    }

    // --- DRAFT ---
    startDraft() {
        const unlocked = CARD_DB.filter(c => c.unlocked);
        this.deck = unlocked.sort(() => 0.5 - Math.random()).slice(0, 4).map(c => ({...c, cd: 0, uid: Math.random()}));
        const container = document.getElementById('draft-cards');
        container.innerHTML = '';
        this.deck.forEach((card, index) => container.appendChild(this.createCardElement(card, 'draft', index)));
        document.getElementById('draft-overlay').classList.remove('hidden');
    }
    toggleDraftCard(index) {
        const idx = this.draftSelection.indexOf(index);
        if (idx > -1) this.draftSelection.splice(idx, 1);
        else if (this.draftSelection.length < 2) this.draftSelection.push(index);
        Array.from(document.getElementById('draft-cards').children).forEach((el, i) => el.classList.toggle('selected-draft', this.draftSelection.includes(i)));
        document.getElementById('btn-confirm-draft').disabled = this.draftSelection.length !== 2;
    }
    confirmDraft() {
        this.draftSelection.forEach(idx => this.hand.push({...this.deck[idx]}));
        document.getElementById('draft-overlay').classList.add('hidden');
        this.state = 'PLAY';
        this.spawnTestEnemies();
        this.render();
    }

    // --- VSTUPY A AKCE HRÁČE ---
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
            if (this.aimTargetCells.some(c => c.x === x && c.y === y) || (this.hand[0]?.aim === 'self' && x === this.player.x && y === this.player.y)) {
                this.executeAim(x, y);
            }
        }
    }
    tryMovePlayer(dx, dy) {
        const nx = this.player.x + dx; const ny = this.player.y + dy;
        if (!this.isValid(nx, ny) || this.getEnemyAt(nx, ny)) return;
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
                if (this.isValid(nx, ny) && !this.getEnemyAt(nx, ny) && !(nx===this.player.x && ny===this.player.y)) {
                    e.x = nx; e.y = ny;
                }
            }
        });
        this.player.screamCd = this.player.maxScreamCd;
        this.endTurn();
    }
    equipCard(uid) {
        if (this.state !== 'PLAY' || this.prepared.length >= this.maxPrepared) return;
        const cardIndex = this.hand.findIndex(c => c.uid === uid);
        if (cardIndex === -1 || this.hand[cardIndex].cd > 0) return;
        this.prepared.push(this.hand.splice(cardIndex, 1)[0]);
        this.endTurn();
    }

    // --- MÍŘENÍ A KARTY ---
    startExecution() {
        if (this.state !== 'PLAY' || this.prepared.length === 0) return;
        this.state = 'AIMING'; this.aimingIndex = 0; this.setupNextAim();
    }
    setupNextAim() {
        if (this.aimingIndex >= this.prepared.length) {
            this.state = 'PLAY'; this.prepared = []; this.endTurn(); return;
        }
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
        this.aimTargetCells = [];
        const card = this.prepared[this.aimingIndex];
        if (!card || card.aim === 'self') return;
        const dir = this.getDirection(this.hoverX, this.hoverY, card.aim);
        if (!dir) return;
        const px = this.player.x, py = this.player.y, dx = dir.dx, dy = dir.dy;

        switch (card.id) {
            case 1: case 2: case 9: this.addAimCell(px+dx, py+dy); break; // Melee/Dash
            case 3: // Hammer
                this.addAimCell(px+dx, py+dy);
                if (dx !== 0) { this.addAimCell(px+dx, py-1); this.addAimCell(px+dx, py+1); }
                else { this.addAimCell(px-1, py+dy); this.addAimCell(px+1, py+dy); }
                break;
            case 4: case 5: // Projectiles
                for(let i=1; i<=5; i++) { const nx=px+dx*i, ny=py+dy*i; if(!this.addAimCell(nx, ny) || this.getEnemyAt(nx,ny)) break; }
                break;
            case 6: // Spear
                let hits=0; for(let i=1; i<=5; i++) { const nx=px+dx*i, ny=py+dy*i; if(!this.addAimCell(nx, ny)) break; if(this.getEnemyAt(nx,ny)) hits++; if(hits>=2) break; }
                break;
            case 8: // Harpoon
                for(let i=1; i<=3; i++) { const nx=px+dx*i, ny=py+dy*i; if(!this.addAimCell(nx, ny) || this.getEnemyAt(nx,ny)) break; }
                break;
            case 10: // Slide
                for(let i=1; i<=2; i++) { const nx=px+dx*i, ny=py+dy*i; if(this.isValid(nx,ny) && !this.getEnemyAt(nx,ny)) this.addAimCell(nx,ny); else break; }
                break;
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

        if ([1, 3, 4, 5, 6].includes(card.id)) { dmgCells.forEach(c => this.damageEntityAt(c.x, c.y, card.dmg)); }
        else if (card.id === 2) {
            let e = this.getEnemyAt(px+dx, py+dy);
            if (e) {
                this.damageEntityAt(e.x, e.y, card.dmg);
                if (e.hp > 0 && this.isValid(e.x+dx, e.y+dy) && !this.getEnemyAt(e.x+dx, e.y+dy) && !(e.x+dx===this.player.x && e.y+dy===this.player.y)) { e.x+=dx; e.y+=dy; }
            }
        }
        else if (card.id === 7) { if (!this.traps.some(t=>t.x===px && t.y===py)) this.traps.push({x:px, y:py}); }
        else if (card.id === 8) {
            let target = dmgCells[dmgCells.length-1];
            if (target) {
                let e = this.getEnemyAt(target.x, target.y);
                if (e) { this.damageEntityAt(e.x, e.y, card.dmg); if(e.hp>0 && this.isValid(px+dx, py+dy) && !this.getEnemyAt(px+dx, py+dy)) {e.x=px+dx; e.y=py+dy;} }
            }
        }
        else if (card.id === 9 || card.id === 10) {
            let target = dmgCells[dmgCells.length-1];
            if (target && !this.getEnemyAt(target.x, target.y)) { this.player.x=target.x; this.player.y=target.y; }
        }

        card.cd = card.maxCd; this.hand.push(card);
        this.aimingIndex++; this.checkTraps(); this.setupNextAim();
    }

    damageEntityAt(x, y, amount) {
        if (this.player.x === x && this.player.y === y) {
            this.player.hp -= amount;
            if (this.player.hp <= 0) this.gameOver();
        } else {
            let e = this.getEnemyAt(x, y);
            if (e) {
                e.hp -= amount;
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

    gameOver() {
        this.state = 'GAMEOVER';
        document.getElementById('gameover-overlay').classList.remove('hidden');
        document.getElementById('go-coins').innerText = this.player.coins;
    }

    // --- AI NEPŘÁTEL ---
    endTurn() {
        if (this.state === 'GAMEOVER') return;
        this.checkTraps();
        this.processEnemiesTurn();
        if (this.state === 'GAMEOVER') return; // Mohl umřít během enemy turn
        
        if (this.player.screamCd > 0) this.player.screamCd--;
        this.hand.forEach(c => { if (c.cd > 0) c.cd--; });
        this.render();
    }

    processEnemiesTurn() {
        // Fáze 1: Exekuce útoků nepřátel, co mířili
        this.enemies.filter(e => e.state === 'AIMING' && !e.stunned).forEach(e => {
            let cells = this.getEnemyAttackCells(e);
            cells.forEach(c => this.damageEntityAt(c.x, c.y, e.dmgAmt));
            e.state = 'READY'; e.aimData = null;
        });

        // Fáze 2: Rozhodování Ready/Equipping
        this.enemies.filter(e => e.hp > 0).forEach(e => {
            if (e.stunned) { e.stunned = false; return; }
            if (e.state === 'EQUIPPING') { e.state = 'READY'; return; }
            if (e.state === 'READY') {
                const p = this.player; const dx = p.x - e.x, dy = p.y - e.y;
                const distO = Math.abs(dx) + Math.abs(dy); const distM = Math.max(Math.abs(dx), Math.abs(dy));

                if (e.type === 'pawn') {
                    if (distO === 1) { e.state = 'AIMING'; e.aimData = {dx, dy}; e.dmgAmt = 2; }
                    else { this.moveAi(e, p.x, p.y, true); }
                } 
                else if (e.type === 'archer') {
                    if (distM <= 2) {
                        if (!this.moveAi(e, p.x, p.y, false)) { // fail flee -> attack
                            let fdx = Math.abs(dx) > Math.abs(dy) ? Math.sign(dx) : 0; let fdy = fdx===0 ? Math.sign(dy) : 0;
                            e.state = 'AIMING'; e.aimData = {dx: fdx, dy: fdy}; e.dmgAmt = 2;
                        }
                    } else {
                        if (e.x === p.x || e.y === p.y) { e.state = 'AIMING'; e.aimData = {dx: Math.sign(dx), dy: Math.sign(dy)}; e.dmgAmt = 2; }
                        else { this.alignAi(e, p.x, p.y); }
                    }
                }
                else if (e.type === 'spearman') {
                    if ((distO === 1 || distO === 2) && (e.x === p.x || e.y === p.y)) { e.state = 'AIMING'; e.aimData = {dx: Math.sign(dx), dy: Math.sign(dy)}; e.dmgAmt = 2; }
                    else { this.moveAi(e, p.x, p.y, true); }
                }
                else if (e.type === 'shaman') {
                    if (distM <= 2) {
                        if (!this.moveAi(e, p.x, p.y, false)) { e.state = 'AIMING'; e.aimData = {dx, dy}; e.dmgAmt = 1; }
                    } else { e.state = 'AIMING'; e.aimData = {dx, dy}; e.dmgAmt = 1; }
                }
            }
        });
    }

    moveAi(enemy, tx, ty, towards) {
        let moves = [ {dx:0, dy:-1}, {dx:0, dy:1}, {dx:-1, dy:0}, {dx:1, dy:0} ];
        let valid = moves.map(m => ({x: enemy.x+m.dx, y: enemy.y+m.dy})).filter(c => this.isValid(c.x, c.y) && !this.getEnemyAt(c.x, c.y) && !(c.x===this.player.x && c.y===this.player.y));
        if (valid.length === 0) return false;

        valid.sort((a, b) => {
            let distA = Math.abs(a.x - tx) + Math.abs(a.y - ty);
            let distB = Math.abs(b.x - tx) + Math.abs(b.y - ty);
            return towards ? distA - distB : distB - distA;
        });
        
        let bestDist = Math.abs(valid[0].x - tx) + Math.abs(valid[0].y - ty);
        let currDist = Math.abs(enemy.x - tx) + Math.abs(enemy.y - ty);
        if (!towards && bestDist <= currDist) return false; // Neumí utéct dál

        enemy.x = valid[0].x; enemy.y = valid[0].y;
        return true;
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
        let cells = [];
        let cx = e.x + e.aimData.dx, cy = e.y + e.aimData.dy;
        
        if (e.type === 'pawn') { if(this.isValid(cx, cy)) cells.push({x:cx, y:cy}); }
        else if (e.type === 'spearman') { 
            if(this.isValid(cx, cy)) cells.push({x:cx, y:cy});
            if(this.isValid(e.x + e.aimData.dx*2, e.y + e.aimData.dy*2)) cells.push({x: e.x + e.aimData.dx*2, y: e.y + e.aimData.dy*2});
        }
        else if (e.type === 'archer') {
            for(let i=1; i<=5; i++) {
                let nx = e.x + e.aimData.dx*i, ny = e.y + e.aimData.dy*i;
                if (!this.isValid(nx, ny)) break;
                cells.push({x:nx, y:ny});
                if (this.getEnemyAt(nx, ny) || (nx === this.player.x && ny === this.player.y)) break; // Zastaví se o kohokoliv
            }
        }
        else if (e.type === 'shaman') {
            let aoe = [{dx:0,dy:0}, {dx:-1,dy:-1}, {dx:1,dy:-1}, {dx:-1,dy:1}, {dx:1,dy:1}];
            aoe.forEach(m => { if(this.isValid(cx+m.dx, cy+m.dy)) cells.push({x: cx+m.dx, y: cy+m.dy}); });
        }
        return cells;
    }

    // --- RENDER ---
    createCardElement(card, context, index) {
        const div = document.createElement('div');
        div.className = `card type-${card.type} ${card.cd > 0 ? 'cooldown' : ''}`;
        if (context === 'prepared' && index === this.aimingIndex && this.state === 'AIMING') div.classList.add('active-aim');
        if (context === 'draft') div.onclick = () => this.toggleDraftCard(index);
        else if (context === 'hand') div.onclick = () => this.equipCard(card.uid);
        div.innerHTML = `${card.cd > 0 ? `<div class="card-cd-overlay">${card.cd}</div>` : ''}<div class="card-header">${card.type}</div><div class="card-name">${card.name}</div><div class="card-stats"><span>🗡️ ${card.dmg}</span><span>⏳ ${card.maxCd}</span></div><div class="card-desc">${card.desc}</div>`;
        return div;
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
                        content += `<div class="entity enemy"><div class="status-icon">${status}</div>${e.svg}<div class="name">${e.name}</div><div class="hp-bar"><div class="hp-fill" style="width: ${(e.hp/e.maxHp)*100}%"></div></div></div>`;
                    }
                }
                cell.innerHTML = content; grid.appendChild(cell);
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