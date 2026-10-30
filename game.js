// Procedurální SVG grafika postav
const SVGs = {
    // Viking: Modro-zlatá rohatá helma, plnovous
    viking: `
        <svg viewBox="0 0 100 100" class="avatar">
            <!-- Tělo/Ramena -->
            <path d="M15,95 Q50,60 85,95" stroke="#b8860b" stroke-width="4" fill="#4682b4" />
            <!-- Vousy -->
            <path d="M30,50 L50,85 L70,50 Z" fill="#d2691e" />
            <!-- Helma (Modrý základ) -->
            <path d="M25,50 C25,20 75,20 75,50 Z" fill="#4682b4" stroke="#b8860b" stroke-width="3"/>
            <!-- Oči -->
            <circle cx="40" cy="55" r="4" fill="#fff"/>
            <circle cx="60" cy="55" r="4" fill="#fff"/>
            <circle cx="40" cy="55" r="1.5" fill="#000"/>
            <circle cx="60" cy="55" r="1.5" fill="#000"/>
            <!-- Rohy (Zlaté) -->
            <path d="M25,45 Q5,30 10,10 Q25,25 35,35" fill="#daa520" stroke="#000" stroke-width="1"/>
            <path d="M75,45 Q95,30 90,10 Q75,25 65,35" fill="#daa520" stroke="#000" stroke-width="1"/>
        </svg>
    `,
    // Ork: Zelený nepřítel pro testování knockbacku
    orc: `
        <svg viewBox="0 0 100 100" class="avatar">
            <!-- Tělo -->
            <path d="M20,95 Q50,70 80,95" stroke="#333" stroke-width="4" fill="#4a5d23" />
            <!-- Hlava -->
            <circle cx="50" cy="45" r="22" fill="#556b2f" stroke="#222" stroke-width="2"/>
            <!-- Oči (Zlé červené) -->
            <polygon points="35,40 45,45 35,48" fill="red"/>
            <polygon points="65,40 55,45 65,48" fill="red"/>
            <!-- Zuby -->
            <path d="M45,58 L50,53 L55,58" stroke="#fff" stroke-width="2" fill="none"/>
        </svg>
    `
};

class Game {
    constructor() {
        this.width = 5;
        this.height = 5;
        this.wave = 1;
        
        // Hráč
        this.player = {
            x: 2, y: 2,
            hp: 5, maxHp: 5,
            coins: 0,
            name: "Viking",
            screamCooldown: 0,
            maxScreamCooldown: 6
        };

        // Testovací nepřátelé pro demonstraci knockbacku
        this.enemies = [
            { id: 1, x: 1, y: 1, hp: 3, maxHp: 3, name: "Orc" },
            { id: 2, x: 2, y: 1, hp: 3, maxHp: 3, name: "Orc" },
            { id: 3, x: 3, y: 2, hp: 3, maxHp: 3, name: "Orc" }
        ];

        this.init();
    }

    init() {
        this.bindInput();
        this.render();
    }

    // --- ZPRACOVÁNÍ VSTUPU ---
    bindInput() {
        document.addEventListener('keydown', (e) => {
            let dx = 0, dy = 0;
            if (e.key === 'ArrowUp' || e.key.toLowerCase() === 'w') dy = -1;
            else if (e.key === 'ArrowDown' || e.key.toLowerCase() === 's') dy = 1;
            else if (e.key === 'ArrowLeft' || e.key.toLowerCase() === 'a') dx = -1;
            else if (e.key === 'ArrowRight' || e.key.toLowerCase() === 'd') dx = 1;

            if (dx !== 0 || dy !== 0) {
                this.tryMovePlayer(dx, dy);
            }
        });
    }

    handleCellClick(x, y) {
        const dx = x - this.player.x;
        const dy = y - this.player.y;
        
        // Pohyb pouze o 1 pole ortogonálně
        if (Math.abs(dx) + Math.abs(dy) === 1) {
            this.tryMovePlayer(dx, dy);
        }
    }

    // --- LOGIKA AKCÍ ---
    tryMovePlayer(dx, dy) {
        const newX = this.player.x + dx;
        const newY = this.player.y + dy;

        // Kontrola hranic
        if (newX < 0 || newX >= this.width || newY < 0 || newY >= this.height) return;

        // Kontrola kolize s nepřítelem (zatím nelze stoupat na nepřátele)
        if (this.getEnemyAt(newX, newY)) {
            // Tady by mohl být klasický bump-attack, pro teď pouze znemožníme pohyb
            return;
        }

        // Provedení pohybu
        this.player.x = newX;
        this.player.y = newY;
        this.endTurn();
    }

    wait() {
        // Hráč vynechá tah
        this.endTurn();
    }

    vikingScream() {
        if (this.player.screamCooldown > 0) return;

        // Najdi všechny nepřátele v okolí 1 pole (ortogonálně i diagonálně)
        this.enemies.forEach(enemy => {
            const dx = enemy.x - this.player.x;
            const dy = enemy.y - this.player.y;

            if (Math.abs(dx) <= 1 && Math.abs(dy) <= 1) {
                // Výpočet směru odhození (Math.sign vrací -1, 0, nebo 1)
                const pushX = Math.sign(dx);
                const pushY = Math.sign(dy);

                const newEx = enemy.x + pushX;
                const newEy = enemy.y + pushY;

                // Zkontroluj, jestli cílové políčko není mimo mapu a jestli je volné
                if (newEx >= 0 && newEx < this.width && newEy >= 0 && newEy < this.height) {
                    if (!this.getEnemyAt(newEx, newEy)) {
                        enemy.x = newEx;
                        enemy.y = newEy;
                    }
                }
            }
        });

        // Nastav cooldown
        this.player.screamCooldown = this.player.maxScreamCooldown;
        this.endTurn();
    }

    // --- SYSTÉM TAHŮ ---
    endTurn() {
        // Snížení cooldownu hráče
        if (this.player.screamCooldown > 0) {
            this.player.screamCooldown--;
        }

        // Zde by v budoucnu následoval tah nepřátel (AI)
        // this.processEnemiesTurn();

        // Překreslení obrazovky
        this.render();
    }

    // --- POMOCNÉ FUNKCE ---
    getEnemyAt(x, y) {
        return this.enemies.find(e => e.x === x && e.y === y);
    }

    getHpBarHtml(hp, maxHp) {
        const percentage = (hp / maxHp) * 100;
        return `
            <div class="hp-bar">
                <div class="hp-fill" style="width: ${percentage}%"></div>
            </div>
        `;
    }

    // --- VYKRESLOVÁNÍ (RENDER) ---
    render() {
        // Aktualizace horní lišty
        document.getElementById('ui-hp').innerText = `${this.player.hp}/${this.player.maxHp}`;
        document.getElementById('ui-coins').innerText = this.player.coins;
        document.getElementById('ui-wave').innerText = this.wave;

        // Aktualizace akčního panelu (Viking Scream)
        const screamBtn = document.getElementById('btn-scream');
        const cdText = document.getElementById('ui-cooldown');
        if (this.player.screamCooldown > 0) {
            screamBtn.disabled = true;
            cdText.innerText = `(${this.player.screamCooldown})`;
        } else {
            screamBtn.disabled = false;
            cdText.innerText = "";
        }

        // Vykreslení mřížky
        const grid = document.getElementById('grid');
        grid.innerHTML = '';

        for (let y = 0; y < this.height; y++) {
            for (let x = 0; x < this.width; x++) {
                const cell = document.createElement('div');
                cell.className = 'cell';
                
                // Zvýraznění políček, kam lze jít (ortogonálně 1 pole)
                const dist = Math.abs(x - this.player.x) + Math.abs(y - this.player.y);
                if (dist === 1) cell.classList.add('adjacent');

                cell.onclick = () => this.handleCellClick(x, y);

                // Kontrola entit
                let content = '';
                
                // Je zde hráč?
                if (this.player.x === x && this.player.y === y) {
                    content = `
                        <div class="entity player">
                            <div class="status-icon">⚔️</div>
                            ${SVGs.viking}
                            <div class="name">${this.player.name}</div>
                            ${this.getHpBarHtml(this.player.hp, this.player.maxHp)}
                        </div>
                    `;
                } else {
                    // Je zde nepřítel?
                    const enemy = this.getEnemyAt(x, y);
                    if (enemy) {
                        content = `
                            <div class="entity enemy">
                                <div class="status-icon">🩸</div>
                                ${SVGs.orc}
                                <div class="name">${enemy.name}</div>
                                ${this.getHpBarHtml(enemy.hp, enemy.maxHp)}
                            </div>
                        `;
                    }
                }

                cell.innerHTML = content;
                grid.appendChild(cell);
            }
        }
    }
}

// Spuštění hry po načtení stránky
let game;
window.onload = () => {
    game = new Game();
};