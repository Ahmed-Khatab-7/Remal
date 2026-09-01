(function () {
    'use strict';
    const API = window.RemalAPI;
    const VOL_ORDER = ['30ML', '50ML', '100ML'];

    // ---- shared state ----
    let allProducts = [];           // ProductListDto[]
    let productMap = {};            // id -> product
    let currentProductId = null;
    let currentCollection = null;
    let currentBundle = null;
    // IDs الحالية للمجموعة/الباقة المفتوحة — لازم تتسجّل عشان الريفريش يقدر يستعيد الصفحة
    // (window.navigate بيحفظها في sessionStorage). من غيرها الريفريش بيرجّع للـ HTML الثابت القديم.
    let currentCollectionId = null;
    let currentBundleId = null;
    let appliedCouponCode = null;
    let appliedCouponDiscount = 0;  // absolute EGP
    let freeShippingThreshold = 2000;
    let catalogReqSeq = 0;

    // ---- helpers ----
    function isRtl() { return document.documentElement.getAttribute('dir') === 'rtl'; }
    function t(ar, en) { return isRtl() ? ar : en; }
    function cur() { return t('ج.م', 'EGP'); }
    // Localized name/inspiredBy helpers — pick English variant when the site is in English.
    function pname(p) {
        if (!p) return '';
        if (isRtl()) return p.name || p.nameEn || '';
        return p.nameEn || p.name || '';
    }
    function pinspired(p) {
        if (!p) return '';
        if (isRtl()) return p.inspiredBy || p.inspiredByEn || '';
        return p.inspiredByEn || p.inspiredBy || '';
    }
    // الوصف/النوتات ثنائية اللغة: تعرض الإنجليزي في وضع EN مع السقوط للعربي عند غيابه
    function pdesc(p) {
        if (!p) return '';
        return isRtl() ? (p.description || p.descriptionEn || '') : (p.descriptionEn || p.description || '');
    }
    function pnote(p, which) {
        if (!p) return '';
        const ar = p['notes' + which] || '', en = p['notes' + which + 'En'] || '';
        return isRtl() ? (ar || en) : (en || ar);
    }
    // اختيار ثنائي اللغة لأي زوج نصّين (يعرض الإنجليزي في وضع EN مع السقوط للعربي والعكس).
    function biln(ar, en) { return isRtl() ? (ar || en || '') : (en || ar || ''); }
    // يفكّ JSON محتوى صفحة التفاصيل (السطر التعريفي + الأكورديونات) القادم من الداشبورد.
    function parseDetailJson(json) { try { const o = JSON.parse(json || '{}'); return (o && typeof o === 'object') ? o : {}; } catch (e) { return {}; } }
    function nl2brEsc(s) { return esc(s == null ? '' : s).replace(/\n/g, '<br>'); }
    // يحدّث عنصر lang-text ديناميكي بقيمتَي عربي/إنجليزي (يحدّث data-ar/data-en + المحتوى الظاهر)
    // فيفضل صح مع تبديل اللغة. مهم: العنصر مشترك بين كل المجموعات/الباقات، فلو القيمة فاضية
    // بنرجّع النص الافتراضي الأصلي (المخزّن مرة واحدة) — مش نسيب محتوى المجموعة السابقة (stale).
    function setBilNode(el, ar, en) {
        if (!el) return false;
        if (el.getAttribute('data-def-ar') === null) {
            el.setAttribute('data-def-ar', el.getAttribute('data-ar') || '');
            el.setAttribute('data-def-en', el.getAttribute('data-en') || '');
        }
        ar = (ar || '').trim(); en = (en || '').trim();
        const arH = ar ? nl2brEsc(ar) : (en ? nl2brEsc(en) : (el.getAttribute('data-def-ar') || ''));
        const enH = en ? nl2brEsc(en) : (ar ? nl2brEsc(ar) : (el.getAttribute('data-def-en') || ''));
        el.setAttribute('data-ar', arH); el.setAttribute('data-en', enH);
        el.innerHTML = isRtl() ? (arH || enH) : (enH || arH);
        return !!(ar || en);
    }
    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])); }

    // ===== تصغير الصور =====
    // صور المنتجات مرفوعة 6000×6000. الملف ٧٠٠ كيلوبايت بس المتصفح بيفك ضغطها في
    // الذاكرة بمقاسها الأصلي: 6000×6000×4 = **١٣٧ ميجابايت للصورة الواحدة**. صفحة
    // بـ ٨ منتجات كانت بتحجز أكتر من ١ جيجا من ذاكرة العرض، وسفاري على الآيفون
    // بيقتل التبويب قبل كده بكتير.
    // /img بيصغّرها لمرة واحدة ويكاشيها: عرض ٤٠٠ = ٠٫٦ ميجا في الذاكرة (أقل ٢٢٠ مرة).
    function imgUrl(url, width) {
        var u = String(url || '').trim();
        if (!u) return '';
        // الصور المحلية (SVG/بديلة) وdata: مش محتاجة تصغير
        if (u.indexOf('data:') === 0 || u.charAt(0) === '/') return u;
        return '/img?w=' + (width || 400) + '&u=' + encodeURIComponent(u);
    }
    window.imgUrl = imgUrl;

    // ===== جودة الصور على الشاشات عالية الكثافة =====
    // بكسل CSS واحد على شاشة الآيفون = ٣ بكسل حقيقية. يعني كارت معروض ٤٠٠ CSS
    // محتاج صورة ١٢٠٠ بكسل عشان تبان حادّة؛ لو خدمناه ٤٠٠ هيتمطّط ويبان مغبّش.
    // srcset بيدّي المتصفح قائمة مقاسات + `sizes` (عرض العنصر المتوقع)، وهو
    // يختار المناسب لكثافة الشاشة وعرض الشاشة — من غير ما نخمّن إحنا.
    function imgSrcset(url, steps) {
        var u = String(url || '').trim();
        if (!u || u.indexOf('data:') === 0 || u.charAt(0) === '/') return '';
        return (steps || [400, 800, 1200]).map(function (w) {
            return imgUrl(u, w) + ' ' + w + 'w';
        }).join(', ');
    }
    window.imgSrcset = imgSrcset;

    // القيم دي مقيسة على الموقع الحيّ مش مقدّرة: عند ٣٩٠ بكسل الكارت ١٦٦ CSS
    // (عمودين = ٤٣vw)، وعند ١٢٨٠ بكسل الكارت ٢٨٦ CSS (٤ أعمدة = ٢٢vw).
    // لو الرقم ده مبالغ فيه المتصفح بيحمّل صورة أكبر من اللازم ويأكل ذاكرة،
    // ولو أقل من اللازم الصورة بتبان مغبّشة — فالدقة هنا مهمة.
    var CARD_SIZES = '(max-width: 600px) 45vw, (max-width: 1024px) 30vw, 300px';
    function money(n) { return Number(n || 0).toLocaleString('en-US'); }
    // Unified toast — always writes into the inner #toast-msg span so the old
    // no-arg showToast() never displays stale text from a previous call.
    function toastMsg(msg) {
        const el = document.getElementById('toast');
        if (!el) return;
        let msgEl = document.getElementById('toast-msg');
        if (!msgEl) {
            msgEl = document.createElement('span');
            msgEl.id = 'toast-msg';
            el.innerHTML = '';
            el.appendChild(msgEl);
        }
        msgEl.textContent = msg;
        el.classList.add('show');
        clearTimeout(toastMsg._t);
        toastMsg._t = setTimeout(() => el.classList.remove('show'), 3200);
    }
    // Back-compat: old showToast() callers now route through toastMsg with a
    // contextual default instead of showing whatever was last written.
    window.showToast = function (msg) { toastMsg(msg || t('تمت الإضافة إلى السلة', 'Added to bag')); };
    function localize(root) {
        const lang = isRtl() ? 'ar' : 'en';
        root.querySelectorAll('.lang-text').forEach(el => {
            const v = el.getAttribute('data-' + lang);
            if (v != null) el.innerHTML = v;
        });
        root.querySelectorAll('[data-placeholder-ar],[data-placeholder-en]').forEach(el => {
            const p = el.getAttribute('data-placeholder-' + lang);
            if (p) el.setAttribute('placeholder', p);
        });
    }
    function sortedSizes(p) {
        return (p.sizes || []).slice().sort((a, b) => VOL_ORDER.indexOf(a.volume) - VOL_ORDER.indexOf(b.volume));
    }
    function defaultSize(p) {
        const ss = sortedSizes(p);
        return ss.find(s => s.volume === '50ML') || ss[0] || { volume: '50ML', price: p.minPrice || 0, stock: 0 };
    }
    // ===== الخصم على المنتج: السعر قبل/بعد =====
    // oldPrice بييجي من الداشبورد لكل حجم. يُعرض مشطوبًا فقط لو أكبر فعلاً من السعر الحالي.
    function hasDiscount(size) {
        if (!size) return false;
        const op = Number(size.oldPrice), p = Number(size.price);
        return isFinite(op) && isFinite(p) && op > p && p > 0;
    }
    function discountPercent(size) {
        if (!hasDiscount(size)) return 0;
        return Math.round((1 - Number(size.price) / Number(size.oldPrice)) * 100);
    }
    // السعر القديم مشطوب جنب السعر الحالي
    function oldPriceHtml(size) {
        if (!hasDiscount(size)) return '';
        return '<span class="old-price en-num">' + money(size.oldPrice) + '</span>';
    }
    // شارة نسبة الخصم (تظهر على صورة الكارت)
    function discountBadgeHtml(size) {
        const pct = discountPercent(size);
        if (!pct) return '';
        return '<span class="card-badge badge-discount">' + (isRtl() ? ('خصم ' + pct + '%') : ('-' + pct + '%')) + '</span>';
    }
    function skeleton(n) {
        let h = '';
        for (let i = 0; i < (n || 4); i++) {
            h += '<div class="noon-card" style="pointer-events:none;"><div class="image-area" style="background:linear-gradient(90deg,#f3f3f3,#ececec,#f3f3f3);"></div>'
               + '<div class="info-area"><div style="height:14px;background:#eee;border-radius:4px;margin-bottom:8px;"></div>'
               + '<div style="height:10px;width:60%;background:#f0f0f0;border-radius:4px;"></div></div></div>';
        }
        return h;
    }
    function gridError(container, msg, retry) {
        if (!container) return;
        container.innerHTML = '<div style="grid-column:1/-1;text-align:center;padding:40px 20px;color:var(--text-muted);">'
            + '<p style="margin-bottom:12px;">' + esc(msg) + '</p>'
            + '<button class="filter-btn" onclick="(' + retry + ')()">' + t('حاول تاني', 'Retry') + '</button></div>';
    }
    function gridEmpty(container, msg) {
        if (!container) return;
        container.innerHTML = '<div style="grid-column:1/-1;text-align:center;padding:40px 20px;color:var(--text-muted);">' + esc(msg) + '</div>';
    }

    // ---- card renderers (reuse existing .noon-card markup/classes) ----
    // Trigger cycle — five slots (4 visible + 1 blank). Guaranteed: no two adjacent
    // cards share the same trigger type. If idx isn't passed (some callers), the
    // module-level counter still gives non-repeating output across the page.
    const TRIGGER_TYPES = ['sold', 'new', 'discount', 'limited', null];
    let _cardSeq = 0;
    function getCardTrigger(product, cardIndex) {
        const i = (typeof cardIndex === 'number') ? cardIndex : (_cardSeq++);
        let typeIndex = i % TRIGGER_TYPES.length;
        if (i > 0) {
            const prevType = TRIGGER_TYPES[(i - 1) % TRIGGER_TYPES.length];
            if (TRIGGER_TYPES[typeIndex] === prevType) {
                typeIndex = (typeIndex + 1) % TRIGGER_TYPES.length;
            }
        }
        const type = TRIGGER_TYPES[typeIndex];
        if (type === 'sold')     return { kind: 'sold',     text: t('تم بيع ' + ((product && product.sold) || Math.floor(10 + (i * 7) % 90)), ((product && product.sold) || Math.floor(10 + (i * 7) % 90)) + ' sold') };
        if (type === 'new')      return { kind: 'new',      text: t('جديد', 'NEW') };
        if (type === 'discount') {
            const pct = (product && product.discountPercent) ? product.discountPercent : (10 + ((i * 7 + 3) % 16));
            return { kind: 'discount', text: t('خصم ' + pct + '٪', pct + '% OFF') };
        }
        if (type === 'limited')  return { kind: 'limited',  text: t('كميات محدودة', 'LIMITED') };
        return null;
    }
    function cardBadge(p, idx) {
        // PRIORITY 1: custom badge set by admin in the dashboard (per-product/bundle/collection)
        if (p && (p.badgeArabic || p.badgeEnglish)) {
            const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
            const text = isRtl ? (p.badgeArabic || p.badgeEnglish) : (p.badgeEnglish || p.badgeArabic);
            const kind = (p.badgeKind || 'sale').toLowerCase();
            const cls = (kind === 'new' || kind === 'limited' || kind === 'bestseller') ? 'badge-new' : 'badge-sale';
            return '<span class="card-badge ' + cls + '">' + esc(text) + '</span>';
        }
        // PRIORITY 2: auto-rotating fallback
        const tr = getCardTrigger(p, idx);
        if (!tr) return '';
        const cls = (tr.kind === 'new' || tr.kind === 'limited') ? 'badge-new' : 'badge-sale';
        const numCls = (tr.kind === 'discount' || tr.kind === 'sold') ? ' en-num' : '';
        return '<span class="card-badge ' + cls + numCls + '">' + tr.text + '</span>';
    }

    // Parse the admin's TickerJson field → array of {ic, txt}. Returns null if absent/invalid.
    function _parseTickerJson(p) {
        if (!p || !p.tickerJson) return null;
        let arr;
        try { arr = JSON.parse(p.tickerJson); } catch (e) { return null; }
        if (!Array.isArray(arr) || !arr.length) return null;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const out = arr.map(item => {
            if (!item) return null;
            const ic = (item.icon || item.ic || 'check').toString();
            const txt = isRtl ? (item.ar || item.text || item.en || '') : (item.en || item.text || item.ar || '');
            const t = (txt || '').toString().trim();
            if (!t) return null;
            return { ic: ic, txt: t };
        }).filter(Boolean).slice(0, 6);
        return out.length ? out : null;
    }

    // Pick custom ticker lines (up to 6) if the admin set any, else fall back to the pool.
    function _customTickerLines(p) {
        if (!p) return null;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const pick = (ar, en) => {
            const a = (ar || '').trim();
            const e = (en || '').trim();
            if (!a && !e) return null;
            return isRtl ? (a || e) : (e || a);
        };
        const all = [
            pick(p.tickerLine1Ar, p.tickerLine1En),
            pick(p.tickerLine2Ar, p.tickerLine2En),
            pick(p.tickerLine3Ar, p.tickerLine3En),
            pick(p.tickerLine4Ar, p.tickerLine4En),
            pick(p.tickerLine5Ar, p.tickerLine5En),
            pick(p.tickerLine6Ar, p.tickerLine6En),
        ].filter(Boolean);
        return all.length ? all : null;
    }
    function _renderTickerLines(lines) {
        // Accept 1–6 unique lines. Add a duplicate of line 1 at the end for seamless loop.
        // The CSS animation chosen depends on the count (vt-Nitems).
        const arr = (lines || []).filter(Boolean).slice(0, 6);
        if (!arr.length) return '';
        const items = arr.concat(arr[0]);
        const cls = 'vt-' + arr.length + 'items';
        return '<div class="vertical-ticker-container"><div class="vertical-ticker ' + cls + '" data-no-normalize="1">'
            + items.map(txt => '<div class="ticker-item">' + (_TICKER_ICONS['check'] || '') + '<span>' + esc(txt) + '</span></div>').join('')
            + '</div></div>';
    }
    // ===== Per-card ticker content =====
    // A small per-product pool of perfume-themed trivia/marketing lines (NOT stock-count).
    // Each card draws 3 lines deterministically (based on its index) so adjacent cards
    // never show the same set, AND the line shown at any moment varies card-to-card.
    const PRODUCT_TICKER_POOL_AR = [
        { ic: 'shipping', txt: 'شحن مجاني فوق ٢٠٠٠ ج.م' },
        { ic: 'return',   txt: 'استبدال متاح خلال ٧ أيام' },
        { ic: 'fast',     txt: 'شحن سريع لجميع المحافظات' },
        { ic: 'check',    txt: 'ثبات يتجاوز ٨ ساعات' },
        { ic: 'check',    txt: 'زيوت عطرية فاخرة · مكونات نقية' },
        { ic: 'fire',     txt: 'تقييمات ٤.٨★ من العملاء' },
        { ic: 'gift',     txt: 'تغليف هدايا أنيق' },
        { ic: 'lab',      txt: 'تركيز Eau de Parfum' },
        { ic: 'cod',      txt: 'الدفع عند الاستلام متاح' },
        { ic: 'support',  txt: 'دعم واتساب ٢٤/٧' },
        { ic: 'leaf',     txt: 'خالٍ من البارابين والكحول الإيثيلي' },
        { ic: 'star',     txt: 'يدوم على البشرة ٨–١٢ ساعة' },
    ];
    const PRODUCT_TICKER_POOL_EN = [
        { ic: 'shipping', txt: 'Free shipping over EGP 2,000' },
        { ic: 'return',   txt: '7-day exchange available' },
        { ic: 'fast',     txt: 'Fast nationwide delivery' },
        { ic: 'check',    txt: '8+ hours of longevity' },
        { ic: 'check',    txt: 'Fine fragrance oils · pure ingredients' },
        { ic: 'fire',     txt: '4.8★ customer rating' },
        { ic: 'gift',     txt: 'Elegant gift wrapping' },
        { ic: 'lab',      txt: 'Eau de Parfum concentration' },
        { ic: 'cod',      txt: 'Cash on delivery available' },
        { ic: 'support',  txt: '24/7 WhatsApp support' },
        { ic: 'leaf',     txt: 'Paraben-free · clean formula' },
        { ic: 'star',     txt: 'Long-lasting 8–12h on skin' },
    ];
    const _TICKER_ICONS = {
        shipping: '<svg class="elite-icon icon-gray" viewBox="0 0 24 24"><rect x="1" y="3" width="15" height="13"></rect><polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon></svg>',
        return:   '<svg class="elite-icon" style="stroke:#7dab6e;" viewBox="0 0 24 24"><polyline points="1 4 1 10 7 10"></polyline><path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10"></path></svg>',
        fast:     '<svg class="elite-icon icon-gray" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>',
        check:    '<svg class="elite-icon icon-green" viewBox="0 0 24 24"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        fire:     '<svg class="elite-icon" style="stroke:#d4af37;" viewBox="0 0 24 24"><path d="M8.5 14.5A2.5 2.5 0 0 0 11 17h2a2.5 2.5 0 0 0 0-5h-1a2.5 2.5 0 0 1 0-5 6.5 6.5 0 0 1 6.5 6.5"></path><path d="M5 21a8 8 0 0 1 8-8"></path></svg>',
        gift:     '<svg class="elite-icon" style="stroke:#c47acc;" viewBox="0 0 24 24"><polyline points="20 12 20 22 4 22 4 12"></polyline><rect x="2" y="7" width="20" height="5"></rect><line x1="12" y1="22" x2="12" y2="7"></line><path d="M12 7H7.5a2.5 2.5 0 0 1 0-5C11 2 12 7 12 7zM12 7h4.5a2.5 2.5 0 0 0 0-5C13 2 12 7 12 7z"></path></svg>',
        lab:      '<svg class="elite-icon" style="stroke:#6fa8c7;" viewBox="0 0 24 24"><path d="M9 2v6L4 18a2 2 0 0 0 1.8 2.9h12.4A2 2 0 0 0 20 18L15 8V2"></path></svg>',
        cod:      '<svg class="elite-icon icon-gray" viewBox="0 0 24 24"><rect x="2" y="6" width="20" height="12" rx="2"></rect><circle cx="12" cy="12" r="2"></circle><path d="M6 12h.01M18 12h.01"></path></svg>',
        support:  '<svg class="elite-icon" style="stroke:#7dab6e;" viewBox="0 0 24 24"><path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z"></path></svg>',
        leaf:     '<svg class="elite-icon" style="stroke:#6da16d;" viewBox="0 0 24 24"><path d="M17 8C8 10 5.9 16.17 3.82 21.34L5.71 22l1-2.3c.5.12 1 .2 1.5.2C19 20 22 3 22 3c-1 2-8 2.25-13 3.25S2 11.5 2 13.5s1.75 3.75 1.75 3.75C7 8 17 8 17 8z"></path></svg>',
        star:     '<svg class="elite-icon" style="stroke:#d4af37;" viewBox="0 0 24 24"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"></polygon></svg>',
    };
    function _pickTickerLinesFor(idx) {
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const pool = isRtl ? PRODUCT_TICKER_POOL_AR : PRODUCT_TICKER_POOL_EN;
        const i = (idx >= 0 ? idx : 0);
        // Stride through the pool so each card gets 3 DISTINCT lines, and starting offset
        // varies per index → adjacent cards have non-overlapping sets.
        const start = (i * 5) % pool.length;
        const a = pool[start];
        const b = pool[(start + 3) % pool.length];
        const c = pool[(start + 7) % pool.length];
        return [a, b, c];
    }
    function _renderPoolTicker(lines) {
        // lines = array of {ic, txt}, 1-6 items. Adds a tail dup of the first for seamless loop.
        const arr = (lines || []).filter(Boolean).slice(0, 6);
        if (!arr.length) return '';
        const items = arr.concat(arr[0]);
        const cls = 'vt-' + arr.length + 'items';
        return '<div class="vertical-ticker-container"><div class="vertical-ticker ' + cls + '" data-no-normalize="1">'
            + items.map(l => '<div class="ticker-item">' + (_TICKER_ICONS[l.ic] || '') + '<span>' + l.txt + '</span></div>').join('')
            + '</div></div>';
    }

    function buildProductTicker(idx, p) {
        if (p) {
            const json = _parseTickerJson(p);
            if (json && json.length) return _renderPoolTicker(json);
            const custom = _customTickerLines(p);
            if (custom && custom.length) return _renderTickerLines(custom);
        }
        return _renderPoolTicker(_pickTickerLinesFor(idx));
    }
    window.buildProductTicker = buildProductTicker;

    function productCardHTML(p, idx) {
        const ds = defaultSize(p);
        const ratingTxt = (p.rating ? Number(p.rating).toFixed(1) : '5.0');
        // شارة الخصم لها الأولوية على الشارة الترويجية العادية — الخصم أهم رسالة للعميل
        const badge = discountBadgeHtml(ds) || cardBadge(p, idx);
        return '<div class="noon-card catalog-item" data-pid="' + p.id + '" data-vol="' + ds.volume + '" data-price="' + ds.price
            + '" data-name="' + esc(p.name) + '" data-nameen="' + esc(p.nameEn) + '" data-img="' + esc(p.imageUrl || '') + '"'
            + ' onclick="openProductDetail(\'' + p.id + '\')">'
            + '<div class="image-area">' + badge
            + '<div class="heart-icon" data-pid="' + p.id + '">♡</div>'
            + '<img src="' + esc(imgUrl(p.imageUrl, 800)) + '" srcset="' + esc(imgSrcset(p.imageUrl)) + '" sizes="' + CARD_SIZES + '" class="product-img" loading="lazy" decoding="async" alt="' + esc(pname(p)) + '"></div>'
            + '<div class="info-area">'
            + '<h3 class="product-title">' + esc(pname(p)) + '</h3>'
            + '<div class="inspired-by">' + (pinspired(p) ? t('مستوحى من: ', 'Inspired by: ') + esc(pinspired(p)) : '') + '</div>'
            + '<div class="rating-pill"><span class="star">★</span><span class="score en-num">' + ratingTxt + '</span>'
            + (p.reviewCount ? '<span class="count en-num">(' + p.reviewCount + ')</span>' : '') + '</div>'
            + '<div class="price-volume-row"><div class="price-area"><span class="currency">' + cur() + '</span>'
            + '<span class="amount en-num">' + money(ds.price) + '</span>'
            + oldPriceHtml(ds) + '</div>'
            + '<div class="volume-tag"><span class="en-num">' + dispVol(ds.volume) + '</span></div></div>'
            + buildProductTicker(idx, p)
            + '<button class="mobile-add-btn" onclick="storefrontAddCard(event)">'
            + '<svg viewBox="0 0 24 24"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"></path><line x1="3" y1="6" x2="21" y2="6"></line></svg>'
            + '<span class="btn-text">' + t('أضِف إلى الحقيبة', 'ADD TO BAG') + '</span></button>'
            + '</div></div>';
    }
    function bundleCardHTML(b, idx) {
        return '<div class="noon-card catalog-item" data-bid="' + b.id + '" data-name="' + esc(b.name) + '" data-nameen="' + esc(b.nameEn || b.name)
            + '" data-price="' + b.finalPrice + '" data-img="' + esc(b.imageUrl || '') + '" onclick="openBundleDetail(\'' + b.id + '\')">'
            + '<div class="image-area"><span class="card-badge badge-sale lang-text" data-ar="باقة" data-en="BUNDLE">' + t('باقة', 'BUNDLE') + '</span>'
            + '<div class="heart-icon" style="display:none;">♡</div>'
            + '<img src="' + esc(imgUrl(b.imageUrl, 800)) + '" srcset="' + esc(imgSrcset(b.imageUrl)) + '" sizes="' + CARD_SIZES + '" class="product-img" loading="lazy" decoding="async" alt="' + esc(pname(b)) + '"></div>'
            + '<div class="info-area"><h3 class="product-title">' + esc(pname(b)) + '</h3>'
            + '<div class="inspired-by">' + esc(biln(b.tag, b.tagEn) || (b.items ? b.items.length + t(' عطور', ' scents') : '')) + '</div>'
            + '<div class="rating-pill"><span class="star">★</span><span class="score en-num">4.9</span></div>'
            + '<div class="price-volume-row"><div class="price-area"><span class="currency">' + cur() + '</span>'
            + '<span class="amount en-num">' + money(b.finalPrice) + '</span>'
            + '<span style="text-decoration:line-through;color:var(--text-muted);font-size:14px;margin-left:8px;" class="en-num">' + money(b.originalPrice) + '</span></div>'
            + '<div class="volume-tag"><span class="en-num">' + (b.items ? b.items.length : 0) + '×</span></div></div>'
            + buildBundleTicker(b, idx)
            + '<button class="mobile-add-btn" onclick="storefrontAddBundleCard(event)">'
            + '<svg viewBox="0 0 24 24"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"></path><line x1="3" y1="6" x2="21" y2="6"></line></svg>'
            + '<span class="btn-text">' + t('ضيف الباقة', 'ADD BUNDLE') + '</span></button>'
            + '</div></div>';
    }
    function collectionCardHTML(c, idx) {
        return '<div class="noon-card catalog-item" data-cid="' + c.id + '" data-name="' + esc(c.name) + '" data-nameen="' + esc(c.nameEn || c.name)
            + '" data-price="' + c.finalPrice + '" data-img="' + esc(c.imageUrl || '') + '" onclick="openCollectionDetail(\'' + c.id + '\')">'
            + '<div class="image-area"><span class="card-badge badge-sale lang-text" data-ar="مجموعة" data-en="SET">' + t('مجموعة', 'SET') + '</span>'
            + '<div class="heart-icon" style="display:none;">♡</div>'
            + '<img src="' + esc(imgUrl(c.imageUrl, 800)) + '" srcset="' + esc(imgSrcset(c.imageUrl)) + '" sizes="' + CARD_SIZES + '" class="product-img" loading="lazy" decoding="async" alt="' + esc(pname(c)) + '"></div>'
            + '<div class="info-area"><h3 class="product-title">' + esc(pname(c)) + '</h3>'
            + '<div class="inspired-by">' + esc(pdesc(c)) + '</div>'
            + '<div class="rating-pill"><span class="star">★</span><span class="score en-num">5.0</span></div>'
            + '<div class="price-volume-row"><div class="price-area"><span class="currency">' + cur() + '</span>'
            + '<span class="amount en-num">' + money(c.finalPrice) + '</span>'
            + (c.originalPrice > c.finalPrice ? '<span style="text-decoration:line-through;color:var(--text-muted);font-size:14px;margin-left:8px;" class="en-num">' + money(c.originalPrice) + '</span>' : '')
            + '</div><div class="volume-tag"><span class="en-num">' + (c.items ? c.items.length : 0) + '×' + (c.sampleVolume || '5ML') + '</span></div></div>'
            + buildCollectionTicker(c, idx)
            + '<button class="mobile-add-btn" onclick="storefrontAddCollectionCard(event)">'
            + '<svg viewBox="0 0 24 24"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"></path><line x1="3" y1="6" x2="21" y2="6"></line></svg>'
            + '<span class="btn-text">' + t('ضيف البوكس', 'ADD SET') + '</span></button>'
            + '</div></div>';
    }
    // Specialized ticker pools per content type
    const BUNDLE_TICKER_POOL_AR = [
        { ic: 'check', txt: '٣ عطور بسعر اتنين' },
        { ic: 'shipping', txt: 'شحن مجاني للباقة كاملة' },
        { ic: 'gift', txt: 'تغليف هدية فاخر مجاناً' },
        { ic: 'star', txt: 'منتقاة بعناية' },
        { ic: 'fire', txt: 'الأكثر طلباً هذا الشهر' },
        { ic: 'return', txt: 'استبدال متاح خلال ٧ أيام' },
        { ic: 'check', txt: 'أحجام كاملة بـ٥٥ مل' },
    ];
    const BUNDLE_TICKER_POOL_EN = [
        { ic: 'check', txt: '3 perfumes for the price of 2' },
        { ic: 'shipping', txt: 'Free shipping on the full bundle' },
        { ic: 'gift', txt: 'Luxury gift wrap included' },
        { ic: 'star', txt: 'Hand-picked by Remal' },
        { ic: 'fire', txt: 'Most-ordered this month' },
        { ic: 'return', txt: '7-day exchange policy' },
        { ic: 'check', txt: 'Full-size 55 ML bottles' },
    ];
    const COLLECTION_TICKER_POOL_AR = [
        { ic: 'check', txt: 'جرّب قبل اقتناء الزجاجة الكاملة' },
        { ic: 'gift', txt: 'بوكس فاخر بتوقيع رمال' },
        { ic: 'shipping', txt: 'شحن مجاني لكل المجموعة' },
        { ic: 'star', txt: 'مثالية كهدية' },
        { ic: 'check', txt: 'كل العينات ٥ مل أصلية' },
        { ic: 'fire', txt: 'بسعر أقل بـ٤٠٪ من العينة الواحدة' },
    ];
    const COLLECTION_TICKER_POOL_EN = [
        { ic: 'check', txt: 'Try before buying full bottles' },
        { ic: 'gift', txt: 'Premium Remal-branded box' },
        { ic: 'shipping', txt: 'Free shipping on the set' },
        { ic: 'star', txt: 'A perfect gift' },
        { ic: 'check', txt: 'All 5ML samples are authentic' },
        { ic: 'fire', txt: '40% cheaper than buying samples one by one' },
    ];
    function _pickFromPool(pool, idx) {
        const i = (idx >= 0 ? idx : 0);
        const start = (i * 5) % pool.length;
        return [pool[start], pool[(start + 3) % pool.length], pool[(start + 5) % pool.length]];
    }
    function buildBundleTicker(b, idx) {
        const json = _parseTickerJson(b);
        if (json && json.length) return _renderPoolTicker(json);
        const custom = _customTickerLines(b);
        if (custom && custom.length) return _renderTickerLines(custom);
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const pool = isRtl ? BUNDLE_TICKER_POOL_AR : BUNDLE_TICKER_POOL_EN;
        const savingsLine = { ic: 'check', txt: isRtl ? ('وفر ' + money(b.savings) + ' ج.م') : ('Save ' + money(b.savings) + ' EGP') };
        return _renderPoolTicker([savingsLine, pool[((idx || 0) * 3) % pool.length], pool[((idx || 0) * 3 + 2) % pool.length]]);
    }
    function buildCollectionTicker(c, idx) {
        const json = _parseTickerJson(c);
        if (json && json.length) return _renderPoolTicker(json);
        const custom = _customTickerLines(c);
        if (custom && custom.length) return _renderTickerLines(custom);
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const pool = isRtl ? COLLECTION_TICKER_POOL_AR : COLLECTION_TICKER_POOL_EN;
        return _renderPoolTicker(_pickFromPool(pool, idx));
    }
    window.buildBundleTicker = buildBundleTicker;
    window.buildCollectionTicker = buildCollectionTicker;

    function fillGrid(id, items, renderer) {
        const el = document.getElementById(id);
        if (!el) return;
        if (!items || !items.length) { gridEmpty(el, t('قريبًا', 'Coming soon')); return; }
        el.innerHTML = items.map(renderer).join('');
        localize(el);
        if (typeof normalizeTickers === 'function') normalizeTickers();
        wireHearts(el);
    }

    // ===== Infinite scroll: يعرض دفعة (٨ = ٤ صفوف) ويحمّل التالي كل ما المستخدم ينزل تحت =====
    // يمسك المصفوفة كاملة في الذاكرة ويكشف منها تدريجيًا (مش صفحات مرقّمة). بيقلّل حجم الـ DOM
    // بشكل كبير (كان بيرسم ٦٠–١٠٠ كارت مرة واحدة) وده بيمنع تثقيل الصفحة والتبييض أثناء السكرول.
    const IG_BATCH = 8;
    function setupInfiniteGrid(gridEl, allItems, renderer, batch) {
        if (!gridEl) return;
        batch = batch || IG_BATCH;
        // فكّ أي مستمع سابق لنفس الجريد (لو الصفحة اترسمت من جديد أو الفلاتر اتغيّرت)
        if (gridEl._igCleanup) { try { gridEl._igCleanup(); } catch (e) {} gridEl._igCleanup = null; }

        gridEl.innerHTML = '';
        if (!allItems || !allItems.length) { gridEmpty(gridEl, t('قريبًا', 'Coming soon')); return; }

        let shown = 0;
        function appendBatch() {
            const slice = allItems.slice(shown, shown + batch);
            if (!slice.length) return;
            gridEl.insertAdjacentHTML('beforeend', slice.map(renderer).join(''));
            shown += slice.length;
            localize(gridEl);
            if (typeof normalizeTickers === 'function') normalizeTickers();
            wireHearts(gridEl);
        }
        function maybeLoadMore() {
            if (shown >= allItems.length) { teardown(); return; }
            // لو آخر الجريد قرّب من نهاية الشاشة (٦٠٠ بكسل) نحمّل الدفعة اللي بعدها،
            // ونكرّر لحد ما الشاشة تتملّي — فمفيش صفحات مرقّمة، بس تحميل مع النزول.
            const rect = gridEl.getBoundingClientRect();
            if (rect.bottom - window.innerHeight < 600) {
                appendBatch();
                if (shown >= allItems.length) { teardown(); return; }
                requestAnimationFrame(maybeLoadMore);
            }
        }
        function onScroll() { maybeLoadMore(); }
        function teardown() {
            window.removeEventListener('scroll', onScroll);
            window.removeEventListener('resize', onScroll);
            if (gridEl._igCleanup === teardown) gridEl._igCleanup = null;
        }
        gridEl._igCleanup = teardown;
        window.addEventListener('scroll', onScroll, { passive: true });
        window.addEventListener('resize', onScroll, { passive: true });
        appendBatch();                          // الدفعة الأولى فورًا (٨ = ٤ صفوف)
        requestAnimationFrame(maybeLoadMore);   // كمّل ملّي الشاشة لو ٨ مش كفاية
    }

    // ---- wishlist hearts ----
    function wireHearts(root) {
        root.querySelectorAll('.heart-icon[data-pid]').forEach(h => {
            const pid = h.getAttribute('data-pid');
            const inList = wishlist.some(w => w.productId === pid);
            h.classList.toggle('liked', inList);
            h.innerHTML = inList ? '❤' : '♡';
            h.onclick = function (e) {
                e.stopPropagation(); e.preventDefault();
                toggleWishlistProduct(pid, h);
            };
        });
    }
    async function toggleWishlistProduct(pid, heartEl) {
        const idx = wishlist.findIndex(w => w.productId === pid);
        const p = productMap[pid];
        const wasInList = idx >= 0;
        if (wasInList) {
            wishlist.splice(idx, 1);
            if (API.isAuthed()) { try { await API.fetch('/wishlist/' + pid, { method: 'DELETE' }); } catch (e) { toastMsg(e.message); return; } }
        } else {
            wishlist.push({
                productId: pid,
                name: p ? p.name : '',
                nameEn: p ? p.nameEn : '',
                price: p ? defaultSize(p).price : 0,
                img: p ? p.imageUrl : '',
                volume: p ? defaultSize(p).volume : '50ML'
            });
            if (API.isAuthed()) { try { await API.fetch('/wishlist/' + pid, { method: 'POST' }); } catch (e) { toastMsg(e.message); return; } }
        }
        saveWishlist();
        updateWishlistBadge();
        toastMsg(wasInList ? t('تم الحذف من المفضلة', 'Removed from wishlist') : t('تمت الإضافة للمفضلة 🤍', 'Added to wishlist 🤍'));
        if (typeof renderWishlistDrawer === 'function') renderWishlistDrawer();
        document.querySelectorAll('.heart-icon[data-pid="' + pid + '"]').forEach(el => {
            const liked = wishlist.some(w => w.productId === pid);
            el.classList.toggle('liked', liked);
            el.innerHTML = liked ? '❤' : '♡';
        });
    }

    // ---- guest cart persistence ----
    function saveGuestCart() { try { localStorage.setItem('remal_guest_cart', JSON.stringify(cart)); } catch (e) {} }
    function loadGuestCart() { try { return JSON.parse(localStorage.getItem('remal_guest_cart') || '[]'); } catch (e) { return []; } }

    async function refreshServerCart() {
        const data = await API.fetch('/cart');
        cart = (data.items || []).map(it => ({
            id: it.id, key: it.id,
            productId: it.productId, bundleId: it.bundleId, collectionId: it.collectionId,
            volume: it.volume, name: it.name, nameEn: it.name,
            img: it.imageUrl, qty: it.quantity, price: it.unitPrice
        }));
    }
    async function mergeGuestCart() {
        const guest = loadGuestCart();
        for (const it of guest) {
            try {
                const body = it.bundleId ? { bundleId: it.bundleId, quantity: it.qty }
                    : it.collectionId ? { collectionId: it.collectionId, quantity: it.qty }
                    : { productId: it.productId, volume: it.volume, quantity: it.qty };
                await API.fetch('/cart/items', { method: 'POST', body: body });
            } catch (e) { /* skip bad item */ }
        }
        try { localStorage.removeItem('remal_guest_cart'); } catch (e) {}
        await refreshServerCart();
    }

    // ---- cart operations (override) ----
    window.addProductToCart = async function (product) {
        const qty = product.qty || 1;
        // تتبع: الإضافة للسلة — أقوى جمهور ريتارجت
        try {
            window.RemalTrack.event('add_to_cart', {
                value: (product.price || 0) * qty,
                items: [{ id: product.productId || product.bundleId || product.collectionId,
                          name: product.nameEn || product.name, variant: product.volume,
                          price: product.price, quantity: qty }]
            });
        } catch (e) {}
        if (API.isAuthed()) {
            try {
                const body = product.bundleId ? { bundleId: product.bundleId, quantity: qty }
                    : product.collectionId ? { collectionId: product.collectionId, quantity: qty }
                    : { productId: product.productId, volume: product.volume, quantity: qty };
                await API.fetch('/cart/items', { method: 'POST', body: body });
                await refreshServerCart();
            } catch (e) { toastMsg(e.message); return; }
        } else {
            const key = product.bundleId || product.collectionId || (product.productId + '|' + product.volume);
            const ex = cart.find(i => i.key === key);
            if (ex) { ex.qty += qty; }
            else {
                cart.push({
                    id: 'L' + Date.now() + Math.random().toString(36).slice(2, 6),
                    key: key, productId: product.productId, bundleId: product.bundleId,
                    collectionId: product.collectionId, volume: product.volume,
                    name: product.name, nameEn: product.nameEn || product.name,
                    price: product.price, img: product.img, qty: qty
                });
            }
            saveGuestCart();
        }
        updateCartBadge();
        renderCartDrawer();
        _refreshCheckoutIfVisible();
        const msg = product.bundleId ? t('تمت إضافة الباقة إلى السلة', 'Bundle added to bag')
                  : product.collectionId ? t('تمت إضافة المجموعة إلى السلة', 'Set added to bag')
                  : t('تمت الإضافة إلى السلة', 'Added to bag');
        toastMsg(msg);
    };
    // لو صفحة الدفع مفتوحة، أي تغيير في السلة (من الدروار أو الإضافة) يتعكس فيها فوراً
    function _refreshCheckoutIfVisible() {
        const co = document.getElementById('checkout');
        if (co && co.classList.contains('active') && typeof window.syncCheckoutFromCart === 'function') {
            window.syncCheckoutFromCart(true);
        }
    }
    window.updateDrawerQty = async function (id, delta) {
        const item = cart.find(i => i.id === id);
        if (!item) return;
        const newQty = item.qty + delta;
        if (API.isAuthed() && !String(id).startsWith('L')) {
            try {
                if (newQty <= 0) await API.fetch('/cart/items/' + id, { method: 'DELETE' });
                else await API.fetch('/cart/items/' + id, { method: 'PUT', body: { quantity: newQty } });
                await refreshServerCart();
            } catch (e) { toastMsg(e.message); return; }
        } else {
            item.qty = newQty;
            if (item.qty <= 0) cart = cart.filter(i => i.id !== id);
            saveGuestCart();
        }
        updateCartBadge();
        renderCartDrawer();
        _refreshCheckoutIfVisible();
    };
    window.removeFromDrawer = async function (id) {
        const el = document.getElementById('ditem_' + id);
        if (el) { el.style.opacity = '0'; el.style.transform = 'translateX(30px)'; el.style.transition = '0.25s ease'; }
        if (API.isAuthed() && !String(id).startsWith('L')) {
            try { await API.fetch('/cart/items/' + id, { method: 'DELETE' }); await refreshServerCart(); }
            catch (e) { toastMsg(e.message); return; }
        } else {
            cart = cart.filter(i => i.id !== id);
            saveGuestCart();
        }
        setTimeout(() => { updateCartBadge(); renderCartDrawer(); _refreshCheckoutIfVisible(); }, 200);
        toastMsg(t('تم حذف المنتج من السلة', 'Item removed from bag'));
    };

    // -------- Bug 7 fix: cart drawer with proper data-id attributes + event delegation --------
    // The original prototype interpolated `onclick="updateDrawerQty('${item.id}', -1)"` — fine for
    // numeric Date.now() ids but broken for the real server GUIDs returned by /cart, because the
    // unquoted hyphens parse as subtraction → silent JS syntax error → dead buttons.
    // This override quotes ids safely AND adds Bug 2's cart-side stock guard.
    function cartItemStockState(item) {
        // Bundles & collections — we don't track per-row stock in the storefront, assume in-stock.
        if (!item.productId) return { available: true, stock: Infinity };
        const p = productMap[item.productId];
        if (!p) return { available: true, stock: Infinity }; // unknown — don't block; backend will reject if invalid
        const sz = (p.sizes || []).find(s => (s.volume || '').toUpperCase() === (item.volume || '').toUpperCase());
        if (!sz) return { available: false, stock: 0 };
        return { available: sz.stock > 0, stock: sz.stock };
    }
    window.renderCartDrawer = function () {
        const body = document.getElementById('cartDrawerBody');
        const footer = document.getElementById('cartDrawerFooter');
        const countEl = document.getElementById('drawerCount');
        if (!body || !footer) return;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const totalQty = cart.reduce((s, i) => s + i.qty, 0);
        const subtotal = cart.reduce((s, i) => s + i.price * i.qty, 0);
        if (countEl) countEl.textContent = totalQty;

        if (cart.length === 0) {
            body.innerHTML = '<div class="cart-drawer-empty">'
                + '<svg viewBox="0 0 24 24"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"/><line x1="3" y1="6" x2="21" y2="6"/></svg>'
                + '<p>' + t('حقيبتك فارغة حتى الآن', 'Your bag is empty') + '</p></div>';
            footer.innerHTML = '<button class="drawer-continue-btn" data-action="close">' + t('اكتشف عطورنا', 'DISCOVER FRAGRANCES') + '</button>';
            wireCartDrawerHandlers();
            return;
        }

        let anyOOS = false;
        body.innerHTML = cart.map(item => {
            const idAttr = String(item.id);
            const stock = cartItemStockState(item);
            const oos = !stock.available;
            if (oos) anyOOS = true;
            const display = isRtl ? item.name : (item.nameEn || item.name);
            const cur = t('ج.م', 'EGP');
            const oosBadge = oos ? '<span class="oos-badge">' + t('نفد المخزون', 'Out of stock') + '</span>' : '';
            return '<div class="drawer-item drawer-item-enter' + (oos ? ' out-of-stock' : '') + '" id="ditem_' + esc(idAttr) + '">'
                + '<img loading="lazy" decoding="async" class="drawer-item-img" src="' + esc(item.img || '') + '" alt="' + esc(display) + '">'
                + '<div class="drawer-item-info">'
                +   '<div class="drawer-item-name">' + esc(display) + oosBadge + '</div>'
                +   '<div class="drawer-item-vol en-num">' + esc(dispVol(item.volume || '')) + '</div>'
                +   '<div class="drawer-item-controls">'
                +     '<div class="drawer-qty">'
                +       '<button class="drawer-qty-btn" type="button" data-action="dec" data-id="' + esc(idAttr) + '" aria-label="' + t('قلل', 'Decrease') + '">−</button>'
                +       '<span class="drawer-qty-val en-num">' + item.qty + '</span>'
                +       '<button class="drawer-qty-btn" type="button" data-action="inc" data-id="' + esc(idAttr) + '" aria-label="' + t('زود', 'Increase') + '"' + (oos ? ' disabled' : '') + '>+</button>'
                +     '</div>'
                +     '<button class="drawer-remove-btn" type="button" data-action="remove" data-id="' + esc(idAttr) + '" title="' + t('حذف', 'Remove') + '">'
                +       '<svg viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/></svg>'
                +     '</button>'
                +   '</div>'
                + '</div>'
                + '<div class="drawer-item-price en-num">' + (item.price * item.qty).toLocaleString('en-US') + ' <span style="font-size:11px;font-weight:600;color:#888;">' + cur + '</span></div>'
                + '</div>';
        }).join('');

        const FREE_SHIPPING_THRESHOLD = (typeof publicSettings === 'object' && publicSettings && publicSettings.freeShippingThreshold) ? publicSettings.freeShippingThreshold : 2000;
        const freeShipping = subtotal >= FREE_SHIPPING_THRESHOLD;
        const checkoutDisabled = anyOOS ? ' disabled' : '';
        footer.innerHTML = ''
            + '<div class="drawer-subtotal">'
            +   '<span class="drawer-subtotal-label">' + t('الإجمالي', 'Subtotal') + '</span>'
            +   '<span class="drawer-subtotal-amount en-num">' + subtotal.toLocaleString('en-US') + ' <span style="font-size:13px;font-weight:600;color:#888">' + t('ج.م', 'EGP') + '</span></span>'
            + '</div>'
            + '<div class="drawer-shipping-note">'
            +   (anyOOS
                  ? ('<span style="color:var(--red);font-weight:700;">' + t('شيل المنتجات اللي نفدت قبل ما تكمل', 'Remove sold-out items before checkout') + '</span>')
                  : (freeShipping
                       ? t('✓ مبروك! شحن مجاني على طلبك', '✓ Congrats! Free shipping on your order')
                       : t('أضف ' + (FREE_SHIPPING_THRESHOLD - subtotal).toLocaleString('en-US') + ' ج.م عشان توصل للشحن المجاني',
                           'Add ' + (FREE_SHIPPING_THRESHOLD - subtotal).toLocaleString('en-US') + ' EGP for free shipping')))
            + '</div>'
            + '<button class="drawer-checkout-btn' + (anyOOS ? ' disabled' : '') + '" type="button" data-action="checkout"' + checkoutDisabled + '>'
            +   '<span>' + t('إتمام الشراء', 'CHECKOUT') + '</span>'
            +   '<svg viewBox="0 0 24 24" style="width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:2.5;stroke-linecap:round;transform:' + (isRtl ? 'rotate(180deg)' : 'none') + '"><path d="M5 12h14M12 5l7 7-7 7"/></svg>'
            + '</button>'
            + '<button class="drawer-continue-btn" type="button" data-action="close">' + t('متابعة التسوق', 'CONTINUE SHOPPING') + '</button>';

        wireCartDrawerHandlers();
    };

    function wireCartDrawerHandlers() {
        const root = document.getElementById('cartDrawer');
        if (!root || root.dataset.wired === '1') return;
        root.dataset.wired = '1';
        root.addEventListener('click', function (e) {
            const btn = e.target.closest('[data-action]');
            if (!btn) return;
            const action = btn.getAttribute('data-action');
            const id = btn.getAttribute('data-id');
            switch (action) {
                case 'dec': updateDrawerQty(id, -1); break;
                case 'inc': updateDrawerQty(id, +1); break;
                case 'remove': removeFromDrawer(id); break;
                case 'close': closeCartDrawer(); break;
                case 'checkout':
                    if (btn.classList.contains('disabled') || btn.disabled) {
                        toastMsg(t('فيه منتجات نفدت في السلة', 'Some items in your bag are out of stock'));
                        return;
                    }
                    closeCartDrawer();
                    navigate('checkout');
                    if (typeof syncCheckoutFromCart === 'function') syncCheckoutFromCart();
                    break;
            }
        });
    }

    // ---- card add buttons ----
    window.storefrontAddCard = function (event) {
        event.stopPropagation(); event.preventDefault();
        const btn = event.currentTarget;
        const card = btn.closest('.noon-card');
        if (!card) return;
        const d = card.dataset;
        addProductToCart({
            productId: d.pid, volume: d.vol, price: Number(d.price),
            name: d.name, nameEn: d.nameen, img: d.img, qty: 1
        });
        animateAddBtn(btn);
    };
    window.storefrontAddBundleCard = function (event) {
        event.stopPropagation(); event.preventDefault();
        const btn = event.currentTarget;
        const d = btn.closest('.noon-card').dataset;
        addProductToCart({ bundleId: d.bid, price: Number(d.price), name: d.name, nameEn: d.nameen, img: d.img, qty: 1 });
        animateAddBtn(btn);
    };
    window.storefrontAddCollectionCard = function (event) {
        event.stopPropagation(); event.preventDefault();
        const btn = event.currentTarget;
        const d = btn.closest('.noon-card').dataset;
        addProductToCart({ collectionId: d.cid, price: Number(d.price), name: d.name, nameEn: d.nameen, img: d.img, qty: 1 });
        animateAddBtn(btn);
    };
    function animateAddBtn(btn) {
        if (typeof addWithBottleAnim === 'function' && btn.querySelector('.bottle-cart-wrap')) {
            addWithBottleAnim(btn);
        } else {
            btn.classList.add('success');
            setTimeout(() => btn.classList.remove('success'), 1600);
        }
    }

    // ---- settings ----
    async function loadSettings() {
        try {
            // Short client cache (60s) so admin changes show up quickly, not after 10 minutes.
            const s = await API.cached('settings', 60000, () => API.fetch('/settings/public'));
            // ===== اللغة الافتراضية للموقع (تُدار من لوحة التحكم) =====
            // بنكاشيها عشان تتطبق قبل أول رسمة في الزيارة الجاية، وبنطبّقها دلوقتي
            // فقط لو الزائر ما اختارش لغة بنفسه (اختياره دايمًا له الأولوية).
            try {
                const def = (s.defaultLanguage === 'en') ? 'en' : 'ar';
                localStorage.setItem('remal_default_lang', def);
                const chosen = localStorage.getItem(LANG_KEY);
                const current = document.documentElement.getAttribute('lang') === 'en' ? 'en' : 'ar';
                if (!chosen && def !== current && typeof applyLanguage === 'function') {
                    applyLanguage(def);
                    // applyLanguage بتحفظ الاختيار — نشيله عشان يفضل "تلقائي"
                    // ويتبع أي تغيير مستقبلي من الأدمن بدل ما يتثبّت على الزائر.
                    localStorage.removeItem(LANG_KEY);
                    if (typeof _repaintActiveStorefrontPage === 'function') _repaintActiveStorefrontPage();
                }
            } catch (e) {}
            freeShippingThreshold = s.freeShippingThreshold || 2000;
            shippingFee = s.shippingFee || 60;
            window._shippingFee = s.shippingFee || 60;
            // أسعار الشحن لكل محافظة (يُدار من الداشبورد) — الافتراضي لأي محافظة غير مذكورة
            try {
                const r = JSON.parse(s.shippingRatesJson || '{}');
                window._shippingRatesRaw = (r && typeof r === 'object' && !Array.isArray(r)) ? r : {};
            } catch (e) { window._shippingRatesRaw = {}; }
            // إعادة بناء المناطق وقائمة المحافظات بعد وصول الإعدادات
            window._shipZones = null;
            if (typeof buildGovernorateOptions === 'function') buildGovernorateOptions();
            // شريط الإعلانات العمودي: قائمة رسائل من لوحة التحكم (announcements_json)،
            // وإلا رسالة واحدة من الإعداد القديم (announcement/announcement_en).
            let annList = [];
            try { annList = JSON.parse(s.announcementsJson || '[]'); } catch (e) {}
            if (!Array.isArray(annList)) annList = [];
            annList = annList.filter(a => a && (a.ar || a.en));
            if (!annList.length && (s.announcementAr || s.announcementEn)) {
                annList = [{ ar: s.announcementAr || s.announcementEn, en: s.announcementEn || s.announcementAr, url: '' }];
            }
            const annInterval = (typeof s.announcementInterval === 'number' && s.announcementInterval >= 2) ? s.announcementInterval : 4;
            if (annList.length) initAnnouncements(annList, annInterval);
            if (typeof calculateOrderSummary === 'function') { try { calculateOrderSummary(); } catch (e) {} }
            // Hero: multi-slide carousel from dashboard (hero_slides_json) — falls back to the
            // single admin image (hero_image_url), then to the neutral CSS gradient.
            // بنكاشي القايمة في localStorage عشان الزيارة الجاية الصور الصح تظهر فورًا
            // قبل ما الـ API يرد — فمفيش ومضة خلفية قديمة/فاضية.
            // كل شريحة = { url: صورة الديسكتوب, mobile: صورة الموبايل (اختيارية) }.
            // الشكل القديم (نص أو {url}) لسه مدعوم — بيتعامل كصورة واحدة للجهازين.
            let slides = [];
            try {
                const parsed = JSON.parse(s.heroSlidesJson || '[]');
                if (Array.isArray(parsed)) {
                    slides = parsed.map(x => {
                        if (typeof x === 'string') return { url: x, mobile: '' };
                        if (x && typeof x === 'object') return { url: (x.url || '').trim(), mobile: (x.mobile || x.urlMobile || '').trim() };
                        return null;
                    }).filter(x => x && x.url);
                }
            } catch (e) {}
            if (!slides.length && s.heroImageUrl) slides = [{ url: s.heroImageUrl, mobile: '' }];
            const heroKey = JSON.stringify(slides);
            try { localStorage.setItem('remal_hero_slides', heroKey); } catch (e) {}
            // متعيدش رسم الكاروسيل لو نفس الصور اترسمت بالفعل من الكاش (عشان ميحصلش وميض/إعادة بدء للعداد)
            if (slides.length && window._heroAppliedKey !== heroKey) {
                initHeroCarousel(slides);
                window._heroAppliedKey = heroKey;
            }
            // الماركي السفلي (يُدار من لوحة التحكم)
            renderHomeMarquee(s.homeMarqueeAr, s.homeMarqueeEn);
            // الأقسام الترويجية (Spotlight) — كاش محلي يظهر فورًا في الزيارة الجاية،
            // ويُمسح فورًا لما الأدمن يحذفها كلها (عشان ميفضلش قسم قديم معلّق بعد الحذف)
            try {
                if (s.promoSectionJson && s.promoSectionJson !== '[]') localStorage.setItem('remal_promo_section', s.promoSectionJson);
                else localStorage.removeItem('remal_promo_section');
            } catch (e) {}
            renderPromoSpotlight(s.promoSectionJson || null);
            // أرقام التحويل (محفظة/إنستا باي) من الداشبورد
            applyPaymentNumbers(s.walletNumber, s.instaPayAddress);
            return s;
        } catch (e) { return null; }
    }

    // ===== Hero Carousel (Vanilla JS) =====
    // شُرَط تقدّم أسفل اليمين: عددها = عدد الصور بالظبط، النشطة بيضاء وتمتلئ ذهبياً
    // على مدة العرض (HERO_SLIDE_MS)، وعند اكتمال الامتلاء يقلب على الصورة التالية تلقائياً.
    const HERO_SLIDE_MS = 6000;
    let _heroIdx = 0, _heroCount = 0;
    function _heroGo(i) {
        const wrap = document.getElementById('heroSlides');
        if (!wrap || !_heroCount) return;
        _heroIdx = (i + _heroCount) % _heroCount;
        [...wrap.children].forEach((el, k) => el.classList.toggle('active', k === _heroIdx));
        // نمط الستوريز: اللي قبل الحالية مضيئة بالكامل (done)، الحالية تمتلئ تدريجياً (active)،
        // واللي بعدها شبه شفافة. عند العودة للأولى يُعاد ضبط الكل.
        document.querySelectorAll('#heroDashes .hero-dash').forEach((d, k) => {
            const fill = d.querySelector('.hero-dash-fill');
            d.classList.remove('active', 'done');
            if (fill) { fill.style.animation = 'none'; fill.style.width = ''; void fill.offsetWidth; fill.style.animation = ''; }
            if (k < _heroIdx) {
                d.classList.add('done');
            } else if (k === _heroIdx) {
                d.classList.add('active');
                // صورة واحدة فقط = شرطة ممتلئة ثابتة بدون تقدم ولا تقليب
                if (fill) fill.style.animationDuration = (_heroCount > 1 ? HERO_SLIDE_MS + 'ms' : '0ms');
                if (fill && _heroCount === 1) fill.style.width = '100%';
            }
        });
    }
    // اختيار صورة الهيرو حسب الجهاز — **بالـ CSS بالكامل**: الـ JS بيحط الرابطين في
    // متغيرين (--hero-d / --hero-m) على العنصر، والميديا كويري في الستايل هي اللي
    // بتختار. كده التبديل فوري ومضمون عند الدوران أو تغيير حجم النافذة بدون أي
    // اعتماد على أحداث resize/matchMedia (اللي ممكن ما تتطلقش في بعض المتصفحات).
    function _applyHeroSrcs() { /* لم يعد له عمل — التبديل صار بالـ CSS. مُبقى للتوافق. */ }
    window._applyHeroSrcs = _applyHeroSrcs;
    function initHeroCarousel(slides) {
        const hero = document.getElementById('heroCarousel');
        const wrap = document.getElementById('heroSlides');
        const dashes = document.getElementById('heroDashes');
        if (!hero || !wrap || !slides || !slides.length) return;
        // توحيد الشكل: نص أو كائن → { url, mobile }
        const urls = slides.map(x => typeof x === 'string' ? { url: x, mobile: '' }
                                   : { url: (x && x.url) || '', mobile: (x && (x.mobile || x.urlMobile)) || '' })
                           .filter(x => x.url || x.mobile);
        if (!urls.length) return;
        _heroCount = urls.length;
        const escq = s => String(s || '').replace(/'/g, '%27').replace(/"/g, '&quot;');
        wrap.innerHTML = urls.map((u, k) => {
            // --hero-m بيتحط بس لو فيه صورة موبايل فعلًا، وإلا الـ CSS بيرجع للديسكتوب
            const d = escq(u.url || u.mobile);
            const m = escq(u.mobile);
            const style = "--hero-d:url('" + d + "');" + (m ? "--hero-m:url('" + m + "');" : '');
            return '<div class="hero-slide' + (k === 0 ? ' active' : '') + '"'
                 + ' data-desktop="' + d + '" data-mobile="' + m + '"'
                 + ' style="' + style + '"></div>';
        }).join('');
        // الشُرَط: واحدة لكل صورة (حتى لو صورة واحدة) — والضغط عليها ينتقل مباشرة
        if (dashes) {
            dashes.innerHTML = urls.map((_, k) =>
                '<button type="button" class="hero-dash" role="tab" aria-label="Slide ' + (k + 1) + '"><span class="hero-dash-fill"></span></button>'
            ).join('');
            dashes.querySelectorAll('.hero-dash').forEach((d, k) => d.addEventListener('click', () => _heroGo(k)));
            // اكتمال امتلاء الشرطة النشطة = وقت التقليب للصورة التالية
            dashes.addEventListener('animationend', e => {
                if (e.animationName === 'heroDashFill' && _heroCount > 1) _heroGo(_heroIdx + 1);
            });
        }
        _heroGo(0);
        // إيقاف مؤقت للتقدم أثناء مرور الماوس (الأنيميشن يتجمد ويكمل من مكانه)
        hero.addEventListener('mouseenter', () => hero.classList.add('hero-paused'));
        hero.addEventListener('mouseleave', () => hero.classList.remove('hero-paused'));
        // السحب باللمس (الاتجاه صحيح في RTL و LTR)
        let _tx = null;
        hero.addEventListener('touchstart', e => { _tx = e.touches[0].clientX; }, { passive: true });
        hero.addEventListener('touchend', e => {
            if (_tx === null) return;
            const dx = e.changedTouches[0].clientX - _tx; _tx = null;
            if (Math.abs(dx) < 45) return;
            const rtl = isRtl();
            // In RTL a right-swipe means "next"
            _heroGo(_heroIdx + ((dx < 0) !== rtl ? 1 : -1));
        }, { passive: true });
    }

    window.initHeroCarousel = initHeroCarousel;

    // ===== شريط الإعلانات العمودي (Vertical Text Carousel) =====
    // رسالة واحدة تظهر، وتتبدّل بحركة انزلاق للأعلى كل (interval) ثانية.
    // كل رسالة: { ar, en, url }. الرابط اختياري ويجعل الرسالة قابلة للنقر.
    let _annMsgs = [], _annIdx = 0, _annTimer = null, _annInterval = 4000;
    function _annCurrentText(m) { return (isRtl() ? (m.ar || m.en) : (m.en || m.ar)) || ''; }
    function _annPaint(m, animate) {
        const line = document.getElementById('announceLine');
        const txt = document.getElementById('announceText');
        if (!line || !txt) return;
        const apply = () => {
            txt.textContent = _annCurrentText(m);
            // خزّن اللغتين على العنصر ليعمل تبديل اللغة بسلاسة
            txt.setAttribute('data-ar', m.ar || m.en || '');
            txt.setAttribute('data-en', m.en || m.ar || '');
            const url = (m.url || '').trim();
            if (url) { line.setAttribute('href', url); line.classList.add('has-link'); line.target = /^https?:/i.test(url) && url.indexOf(location.host) === -1 ? '_blank' : '_self'; }
            else { line.removeAttribute('href'); line.classList.remove('has-link'); }
        };
        if (!animate) { apply(); return; }
        // انزلاق: الحالية تخرج للأعلى، ثم الجديدة تدخل من الأسفل
        line.classList.add('anim-out');
        setTimeout(() => {
            apply();
            line.classList.remove('anim-out');
            line.classList.add('anim-in-prep');
            void line.offsetHeight;           // إجبار reflow
            line.classList.remove('anim-in-prep');  // يعود لموضعه بحركة الانتقال
        }, 450);
    }
    function _annTick() {
        if (_annMsgs.length < 2) return;
        _annIdx = (_annIdx + 1) % _annMsgs.length;
        _annPaint(_annMsgs[_annIdx], true);
    }
    function initAnnouncements(list, intervalSeconds) {
        _annMsgs = (list || []).filter(m => m && (m.ar || m.en));
        if (!_annMsgs.length) return;
        _annInterval = Math.max(2000, (intervalSeconds || 4) * 1000);
        _annIdx = 0;
        clearInterval(_annTimer);
        _annPaint(_annMsgs[0], false);
        if (_annMsgs.length > 1) _annTimer = setInterval(_annTick, _annInterval);
    }
    // عند تبديل اللغة: أعد رسم الرسالة الحالية باللغة الجديدة فوراً
    window._repaintAnnouncement = function () { if (_annMsgs.length) _annPaint(_annMsgs[_annIdx], false); };
    window.initAnnouncements = initAnnouncements;

    // ===== الماركي السفلي: نص من إعدادَي home_marquee / home_marquee_en =====
    function renderHomeMarquee(arText, enText) {
        const bar = document.getElementById('homeMarquee');
        const track = document.getElementById('homeMarqueeTrack');
        if (!bar || !track) return;
        const txt = isRtl() ? (arText || enText) : (enText || arText);
        if (!txt) { bar.hidden = true; return; }
        bar.hidden = false;
        // نكرر النص لعمل حلقة مستمرة سلسة (النصفان متطابقان للـ -50% translate)
        const item = '<span class="home-marquee-item">' + esc(txt) + '<span class="sep">✦</span></span>';
        track.innerHTML = item.repeat(8);
        // خزّن النصين لإعادة الرسم عند تبديل اللغة
        bar.dataset.ar = arText || ''; bar.dataset.en = enText || '';
    }
    window.renderHomeMarquee = renderHomeMarquee;

    // ===== الأقسام الترويجية (Spotlight) =====
    // promo_section_json = مصفوفة شرائح: [{ enabled, imageUrl, headlineAr, headlineEn,
    //   subAr, subEn, buttonTextAr, buttonTextEn, targetPage, targetId }, ...]
    // توافق خلفي: الشكل القديم (كائن واحد) يُعامل كمصفوفة من عنصر واحد.
    // القسم بيختفي تمامًا لو مفيش ولا شريحة صالحة — فالحذف من الداشبورد بيشيله فورًا،
    // وصورة بايظة عمرها ما تسيب بلوك فاضي (بنشيل شريحتها بعد فشل التحميل).
    let _promoCfg = null;
    function _promoOpenTarget(it) {
        const page = it.targetPage || 'perfumes';
        const id = String(it.targetId || '').trim();
        if (id && page === 'product-detail' && typeof openProductDetail === 'function') return openProductDetail(id);
        if (id && page === 'bundle-detail' && typeof openBundleDetail === 'function') return openBundleDetail(id);
        if (id && page === 'collection-detail' && typeof openCollectionDetail === 'function') return openCollectionDetail(id);
        navigate(page);
    }
    function renderPromoSpotlight(json) {
        const sec = document.getElementById('promoSpotlight');
        const track = document.getElementById('promoTrack');
        if (!sec || !track) return;
        let items = [];
        try {
            const o = JSON.parse(json || 'null');
            if (Array.isArray(o)) items = o;
            else if (o && typeof o === 'object') items = [o];
        } catch (e) {}
        items = items.filter(it => it && it.enabled !== false && it.imageUrl && String(it.imageUrl).trim());
        _promoCfg = items.length ? items : null;
        track.innerHTML = '';
        if (!items.length) { sec.hidden = true; return; }
        const rtl = isRtl();
        items.forEach(it => {
            const slide = document.createElement('div');
            slide.className = 'promo-slide';
            const imgUrl = String(it.imageUrl).trim();
            slide.style.backgroundImage = "url('" + imgUrl.replace(/'/g, '%27') + "')";
            const content = document.createElement('div');
            content.className = 'promo-slide-content';
            const h = document.createElement('h2');
            h.className = 'promo-slide-title';
            h.textContent = (rtl ? (it.headlineAr || it.headlineEn || '') : (it.headlineEn || it.headlineAr || '')).trim();
            const p = document.createElement('p');
            p.className = 'promo-slide-sub';
            p.textContent = (rtl ? (it.subAr || it.subEn || '') : (it.subEn || it.subAr || '')).trim();
            const b = document.createElement('button');
            b.type = 'button'; b.className = 'btn-luxury promo-slide-btn';
            b.textContent = (rtl ? (it.buttonTextAr || '') : (it.buttonTextEn || '')).trim() || (rtl ? 'اقتنِ الآن' : 'SHOP NOW');
            b.onclick = function () { _promoOpenTarget(it); };
            content.appendChild(h); content.appendChild(p); content.appendChild(b);
            slide.appendChild(content);
            track.appendChild(slide);
            const probe = new Image();
            probe.onerror = function () { slide.remove(); if (!track.children.length) sec.hidden = true; };
            probe.src = imgUrl;
        });
        sec.hidden = false;
        try { track.scrollTo({ left: 0 }); } catch (e) { track.scrollLeft = 0; }
    }
    window.renderPromoSpotlight = renderPromoSpotlight;
    // إعادة رسم النصوص عند تبديل اللغة (الصور والوجهات كما هي)
    window.refreshPromoSpotlightLang = function () {
        if (_promoCfg) renderPromoSpotlight(JSON.stringify(_promoCfg));
    };

    // ===== أرقام التحويل (محفظة / إنستا باي) من الداشبورد =====
    // العميل لازم يشوف الرقم الصح اللي يحوّل عليه — فبقى يُدار من لوحة التحكم بدل ما يكون ثابت.
    function applyPaymentNumbers(wallet, insta) {
        try {
            if (wallet) {
                document.querySelectorAll('[data-pay-wallet]').forEach(el => { el.textContent = wallet; });
                window._payWallet = wallet;
            }
            if (insta) {
                document.querySelectorAll('[data-pay-insta]').forEach(el => { el.textContent = insta; });
                window._payInsta = insta;
            }
        } catch (e) {}
    }
    window.applyPaymentNumbers = applyPaymentNumbers;

    // ===== تحويل شبكات الهوم الثلاث إلى سلايدرز أفقية =====
    // نلفّ كل grid في shell بأسهم يمين/يسار — نفس عنصر الـ grid (بنفس الـ id) ينتقل
    // داخل الـ shell، فكل دوال fillGrid/render تستمر في العمل بدون أي تغيير.
    function enhanceHomeSliders() {
        ['homeCultGrid', 'homeNewGrid', 'homeBundlesGrid'].forEach(id => {
            const grid = document.getElementById(id);
            if (!grid || grid.classList.contains('h-slider')) return;
            grid.classList.add('h-slider');
            const shell = document.createElement('div');
            shell.className = 'h-slider-shell';
            grid.parentNode.insertBefore(shell, grid);
            shell.appendChild(grid);
            const mk = (side, dir) => {
                const b = document.createElement('button');
                b.type = 'button';
                b.className = 'hs-arrow hs-arrow-' + side;
                b.setAttribute('aria-label', side === 'left' ? 'Scroll left' : 'Scroll right');
                b.innerHTML = side === 'left'
                    ? '<svg viewBox="0 0 24 24"><polyline points="15 18 9 12 15 6"/></svg>'
                    : '<svg viewBox="0 0 24 24"><polyline points="9 18 15 12 9 6"/></svg>';
                b.addEventListener('click', () => {
                    // dir = الاتجاه الفيزيائي (يسار = سالب) — يعمل في RTL و LTR
                    grid.scrollBy({ left: dir * Math.round(grid.clientWidth * 0.85), behavior: 'smooth' });
                });
                shell.appendChild(b);
            };
            mk('left', -1); mk('right', 1);
        });
    }
    document.addEventListener('DOMContentLoaded', enhanceHomeSliders);
    if (document.readyState !== 'loading') enhanceHomeSliders();

    // ===== حجم الزجاجة للعرض فقط: 50 → 55 مل =====
    // البيانات الداخلية (السلة/الطلبات) تحتفظ بمعرّف "50ML" كما هو في قاعدة البيانات؛
    // التحويل هنا للعرض على الشاشة فقط حتى لا تنكسر مطابقة المقاسات في الباك إند.
    function dispVol(v) {
        const s = String(v || '').trim();
        if (/^50\s*ML$/i.test(s)) return '55 ML';
        return s.replace(/ML$/i, ' ML').replace(/\s{2,}/g, ' ');
    }
    window.dispVol = dispVol;

    // ---- home + catalog ----
    // Returns only live products (server-side soft-deletes are already filtered out by the API's
    // global query filter, but we double-check here for defense in depth + handle the legacy
    // `isDeleted` / `status === 'Deleted'` shapes too).
    function isLiveProduct(p) {
        if (!p) return false;
        if (p.isDeleted === true) return false;
        if (p.status === 'Deleted' || p.status === 'Archived') return false;
        return true;
    }
    let _productsFetchedAt = 0;
    const PRODUCTS_TTL_MS = 5 * 60 * 1000; // 5 minutes
    async function loadProductsBase(force) {
        const now = Date.now();
        if (!force && allProducts.length && (now - _productsFetchedAt) < PRODUCTS_TTL_MS) {
            return allProducts;
        }
        const data = await API.fetch('/products?pageSize=100');
        allProducts = (data.items || []).filter(isLiveProduct);
        productMap = {};
        allProducts.forEach(p => { productMap[p.id] = p; });
        _productsFetchedAt = now;
        return allProducts;
    }
    // Background refresh every 5 minutes so deletions/edits made in the dashboard
    // propagate to an open storefront without the user having to reload.
    setInterval(() => {
        loadProductsBase(true).then(() => {
            // re-paint any currently-visible product grid
            const active = document.querySelector('.page-section.active');
            if (!active) return;
            if (active.id === 'home' && typeof renderHome === 'function') renderHome();
            else if (active.id === 'perfumes' && typeof renderCatalog === 'function') renderCatalog();
            else if (active.id === 'bundles' && typeof renderBundlesPage === 'function') renderBundlesPage();
            else if (active.id === 'collections' && typeof renderCollectionsPage === 'function') renderCollectionsPage();
        }).catch(() => { /* offline / API down — keep stale cache */ });
    }, PRODUCTS_TTL_MS);
    // Also expose a manual invalidator the dashboard can fire via storage event,
    // so a delete in another tab clears the storefront cache instantly AND repaints
    // the currently-visible page so the user sees the change without a refresh.
    function _repaintActiveStorefrontPage() {
        const active = document.querySelector('.page-section.active');
        if (!active) return;
        try {
            if (active.id === 'home' && typeof renderHome === 'function') renderHome();
            else if (active.id === 'all-products' && typeof renderAllProductsPage === 'function') renderAllProductsPage();
            else if (active.id === 'perfumes' && typeof renderCatalog === 'function') renderCatalog();
            else if (active.id === 'bundles' && typeof renderBundlesPage === 'function') renderBundlesPage();
            else if (active.id === 'collections' && typeof renderCollectionsPage === 'function') renderCollectionsPage();
            else if (active.id === 'product-detail' && typeof currentProductId !== 'undefined' && currentProductId && typeof openProductDetail === 'function') openProductDetail(currentProductId);
            else if (active.id === 'bundle-detail' && currentBundle && typeof renderBundleDetail === 'function') renderBundleDetail(currentBundle);
            else if (active.id === 'collection-detail' && currentCollection && typeof renderCollectionDetail === 'function') renderCollectionDetail(currentCollection);
            // ⚠️ صفحة الدفع كانت ناقصة من القايمة دي — وده كان سبب إن السلة
            // ما بتتحوّلش للإنجليزي عند تبديل اللغة وإنت واقف عليها. النصوص
            // نفسها مترجمة صح (t / isRtl)، لكن الترجمة بتتنفّذ **وقت الرسم**،
            // ولو ما أعدناش الرسم بيفضل ظاهر آخر نص اترسم — يعني "حقيبتك فارغة"
            // و"مجاني" بيفضلوا بالعربي وسط صفحة إنجليزي.
            else if (active.id === 'checkout' && typeof window.syncCheckoutFromCart === 'function') window.syncCheckoutFromCart(true);
        } catch (e) { /* swallow — keep stale view */ }

        // درج السلة الجانبي مستقل عن الصفحات وبيتبني بالـ innerHTML كذلك،
        // فمحتاج إعادة رسم صريحة مهما كانت الصفحة النشطة.
        try { if (typeof renderCartDrawer === 'function') renderCartDrawer(); } catch (e) {}
        // ملخّص الطلب أرقامه ونصوصه ("مجاني"/"FREE") بتتكتب من جافاسكريبت كمان.
        try { if (typeof calculateOrderSummary === 'function') calculateOrderSummary(); } catch (e) {}
    }
    // مهم: toggleLanguage معرّفة في سكربت بلوك تاني (خارج الـ IIFE دي) — لازم نصدّر الدالة
    // على window وإلا إعادة رسم المحتوى الديناميكي عند تبديل اللغة تتخطى بصمت.
    window._repaintActiveStorefrontPage = _repaintActiveStorefrontPage;
    window.addEventListener('storage', (e) => {
        if (e.key === 'ramal_products_invalidate') {
            loadProductsBase(true).then(_repaintActiveStorefrontPage).catch(() => {});
        }
        else if (e.key === 'ramal_order_status_changed') {
            // Customer is on the tracking page with this exact order code visible? Re-fetch.
            try {
                const payload = JSON.parse(e.newValue || '{}');
                const code = payload && payload.code;
                if (!code) return;
                // (a) tracking page has the result box open for this code → refresh it
                const box = document.getElementById('trackingResultBox');
                const idEl = document.getElementById('resOrderId');
                if (box && box.classList.contains('show') && idEl && idEl.textContent === code) {
                    if (typeof trackOrderNow === 'function') {
                        const input = document.getElementById('trackInput');
                        if (input) input.value = code;
                        trackOrderNow();
                        toastMsg(t('تم تحديث حالة الطلب', 'Order status updated'));
                    }
                }
                // (b) order-success page is showing this code → flash a small notice
                const dispIdEl = document.getElementById('displayOrderId');
                const onSuccess = document.querySelector('#order-success.page-section.active') || document.querySelector('#order-success.active');
                if (onSuccess && dispIdEl && dispIdEl.textContent === code) {
                    toastMsg(t('تحديث: ', 'Update: ') + (payload.status || ''));
                }
            } catch (err) { /* ignore */ }
        }
    });
    async function renderHome() {
        ['homeCultGrid', 'homeNewGrid'].forEach(id => { const e = document.getElementById(id); if (e) e.innerHTML = skeleton(4); });
        try {
            await loadProductsBase();
            const featured = await API.cached('featured', 600000, () => API.fetch('/products/featured'));
            const byIds = ids => (ids || []).map(id => productMap[id]).filter(Boolean);
            const cult = featured[0] ? byIds(featured[0].productIds) : allProducts.slice(0, 4);
            const fresh = featured[1] ? byIds(featured[1].productIds) : allProducts.slice(0, 4);
            fillGrid('homeCultGrid', cult, productCardHTML);
            fillGrid('homeNewGrid', fresh, productCardHTML);
        } catch (e) {
            gridError(document.getElementById('homeCultGrid'), e.message, 'renderHome');
        }
        renderBundlesInto(['homeBundlesGrid']);
        renderCollectionsInto(['homeOffersGrid']);
    }
    // clientSort: 'price-asc' | 'price-desc' | null
    // (the API has no price-sort, so price ordering is done client-side here)
    async function renderCatalog(query, clientSort) {
        const el = document.getElementById('catalogGrid');
        if (!el) return;
        el.innerHTML = skeleton(8);
        const seq = ++catalogReqSeq;
        try {
            const data = await API.fetch('/products?pageSize=60' + (query || ''));
            if (seq !== catalogReqSeq) return;
            let items = data.items || [];
            items.forEach(p => { if (!productMap[p.id]) productMap[p.id] = p; });
            if (clientSort === 'price-asc') items = items.slice().sort((a, b) => (a.minPrice || 0) - (b.minPrice || 0));
            else if (clientSort === 'price-desc') items = items.slice().sort((a, b) => (b.minPrice || 0) - (a.minPrice || 0));
            if (!items.length) { gridEmpty(el, t('قريبًا', 'Coming soon')); return; }
            el.innerHTML = items.map(productCardHTML).join('');
            localize(el);
            if (typeof normalizeTickers === 'function') normalizeTickers();
            wireHearts(el);
        } catch (e) {
            if (seq === catalogReqSeq) gridError(el, e.message, 'renderCatalog');
        }
    }
    let bundlesCache = null, collectionsCache = null;
    async function renderBundlesInto(ids) {
        ids.forEach(id => { const e = document.getElementById(id); if (e) e.innerHTML = skeleton(4); });
        try {
            if (!bundlesCache) { const d = await API.fetch('/bundles?pageSize=50'); bundlesCache = d.items || []; }
            ids.forEach(id => fillGrid(id, bundlesCache, bundleCardHTML));
        } catch (e) {
            ids.forEach(id => gridError(document.getElementById(id), e.message, 'renderBundlesPage'));
        }
    }
    async function renderCollectionsInto(ids) {
        ids.forEach(id => { const e = document.getElementById(id); if (e) e.innerHTML = skeleton(3); });
        try {
            if (!collectionsCache) { collectionsCache = await API.fetch('/collections'); }
            ids.forEach(id => fillGrid(id, collectionsCache, collectionCardHTML));
        } catch (e) {
            ids.forEach(id => gridError(document.getElementById(id), e.message, 'renderCollectionsPage'));
        }
    }
    // ===== صفحة «كل المنتجات» =====
    // بتعيد استخدام نفس دوال الرسم ونفس الكاش بتاع باقي الصفحات — فمفيش طلبات
    // شبكة زيادة ولا نسخة تانية من منطق الكروت تحتاج صيانة منفصلة.
    async function renderAllProductsPage() {
        const pg = document.getElementById('apPerfumesGrid');
        if (pg) {
            pg.innerHTML = skeleton(8);
            try {
                await loadProductsBase();
                fillGrid('apPerfumesGrid', allProducts, productCardHTML);
            } catch (e) { gridError(pg, e.message, 'renderAllProductsPage'); }
        }
        renderBundlesInto(['apBundlesGrid']);
        renderCollectionsInto(['apCollectionsGrid']);
    }
    window.renderAllProductsPage = renderAllProductsPage;

    // صفحات القوائم الكاملة — infinite scroll (٨ لكل دفعة). بتاخد نسخة جديدة من السيرفر
    // (force) عشان تتخلّص من أي كروت placeholder ثابتة أو داتا قديمة متخزّنة.
    async function renderBundlesFullPage(force) {
        const el = document.getElementById('bundlesGrid');
        if (!el) return;
        el.innerHTML = skeleton(8);
        try {
            if (force || !bundlesCache) { const d = await API.fetch('/bundles?pageSize=50'); bundlesCache = d.items || []; }
            setupInfiniteGrid(el, bundlesCache, bundleCardHTML, IG_BATCH);
        } catch (e) { gridError(el, e.message, 'renderBundlesPage'); }
    }
    async function renderCollectionsFullPage(force) {
        const el = document.getElementById('collectionsPageGrid');
        if (!el) return;
        el.innerHTML = skeleton(8);
        try {
            if (force || !collectionsCache) { collectionsCache = await API.fetch('/collections'); }
            setupInfiniteGrid(el, collectionsCache, collectionCardHTML, IG_BATCH);
        } catch (e) { gridError(el, e.message, 'renderCollectionsPage'); }
    }
    window.renderHome = renderHome;
    window.renderCatalog = function () { if (typeof applyCatalogFilters === 'function') applyCatalogFilters(); else renderCatalog(''); };
    window.renderBundlesPage = function () { renderBundlesInto(['homeBundlesGrid']); renderBundlesFullPage(); };
    window.renderCollectionsPage = function () { renderCollectionsInto(['homeOffersGrid']); renderCollectionsFullPage(); };

    // ---- catalog filters (legacy entry kept for outside callers) ----
    window.filterCatalog = function (type, btn) {
        // Map legacy single-button calls onto the new filter state
        if (type === 'all')        { catalogFilterState.category = 'all'; catalogFilterState.sort = 'newest'; }
        else if (type === 'bestseller') { catalogFilterState.sort = 'bestseller'; }
        else if (type === 'new')        { catalogFilterState.sort = 'newest'; }
        else if (type === 'men')        { catalogFilterState.category = 'men'; }
        else if (type === 'unisex')     { catalogFilterState.category = 'unisex'; }
        else if (type === 'price-low')  { catalogFilterState.sort = 'price-asc'; }
        else if (type === 'price-high') { catalogFilterState.sort = 'price-desc'; }
        applyCatalogFilters();
    };

    // ================ Pro filter state + driver ================
    // Multi-select filters: each `categories/volumes/families/occasions` is an ARRAY.
    // Empty array = no filter (match all). Single-select: `rating` (number) + `sort` (string).
    const SORT_LABELS_AR = { newest: 'الأحدث', bestseller: 'الأكثر مبيعاً', 'price-asc': 'السعر: من الأقل', 'price-desc': 'السعر: من الأعلى' };
    const SORT_LABELS_EN = { newest: 'Newest', bestseller: 'Best Sellers', 'price-asc': 'Price: Low to High', 'price-desc': 'Price: High to Low' };
    const CAT_LABELS_AR = { men: 'رجالي', women: 'نسائي', unisex: 'للجنسين' };
    const CAT_LABELS_EN = { men: 'Men', women: 'Women', unisex: 'Unisex' };
    const FAM_LABELS_AR = { oud:'عود', rose:'ورد', musk:'مسك', vanilla:'فانيليا', amber:'عنبر', citrus:'حمضيات', woody:'خشبي', oriental:'شرقي', fresh:'منعش', sweet:'حلو', floral:'زهري', spicy:'حار' };
    const FAM_LABELS_EN = { oud:'Oud', rose:'Rose', musk:'Musk', vanilla:'Vanilla', amber:'Amber', citrus:'Citrus', woody:'Woody', oriental:'Oriental', fresh:'Fresh', sweet:'Sweet', floral:'Floral', spicy:'Spicy' };
    const OCC_LABELS_AR = { daily:'يومي', evening:'سهرات', office:'شغل', special:'مناسبات خاصة', gift:'هدية' };
    const OCC_LABELS_EN = { daily:'Daily', evening:'Evening', office:'Office', special:'Special', gift:'Gift' };
    // Arabic + English keywords that mark a product as belonging to a fragrance family.
    // Matched against name, inspiredBy, description, notesTop/Heart/Base (case-insensitive).
    const FAMILY_KEYWORDS = {
        oud:      ['عود','بخور','agar','oud'],
        rose:     ['ورد','جوري','rose'],
        musk:     ['مسك','musk'],
        vanilla:  ['فانيليا','vanilla'],
        amber:    ['عنبر','amber'],
        citrus:   ['ليمون','برتقال','جريب','حمضيات','citrus','lemon','orange','bergamot','grapefruit','lime'],
        woody:    ['خشب','صندل','أرز','أبنوس','wood','sandalwood','cedar'],
        oriental: ['شرقي','بخور','توابل','oriental','spices'],
        fresh:    ['منعش','منعشة','بحر','صيف','شاي أخضر','fresh','marine','aquatic','summer'],
        sweet:    ['حلو','كراميل','شوكولاتة','عسل','sweet','caramel','chocolate','honey','gourmand'],
        floral:   ['ياسمين','زهر','زنبق','أوركيد','جردينيا','floral','jasmine','lily','orchid','gardenia','tuberose'],
        spicy:    ['حار','توابل','زنجبيل','هيل','قرفة','spicy','spice','ginger','cardamom','cinnamon','pepper']
    };
    // Same idea for occasion — matches keywords in description/notes/inspiredBy.
    const OCCASION_KEYWORDS = {
        daily:   ['يومي','نهاري','للشغل','للجامعة','daily','everyday','casual'],
        evening: ['سهرة','سهرات','ليلي','مساء','evening','night'],
        office:  ['شغل','مكتب','رسمي','office','work','formal'],
        special: ['مناسبة','حفلة','زفاف','جواز','special','event','occasion','wedding'],
        gift:    ['هدية','هدايا','gift']
    };
    const PRICE_MIN = 0, PRICE_MAX = 2000; // kept for legacy refs; price is now free-form min/max
    const catalogFilterState = {
        categories: [], // array of 'men'|'women'|'unisex'
        volumes: [],    // (unused in simple bar) array of '30ML'|'50ML'|'100ML'
        families: [],   // (unused) array of family keys
        occasions: [],  // (unused) array of occasion keys
        minRating: 0,   // (unused) 0 = any
        inStockOnly: false,
        sort: 'newest',
        sortTouched: false, // true once the user explicitly picks a sort pill
        priceMin: null, // null = no minimum
        priceMax: null, // null = no maximum
    };

    // ---- URL persistence ----
    function readCatalogFiltersFromURL() {
        try {
            const u = new URL(window.location.href);
            const arr = (k, allow) => { const v = u.searchParams.get(k); if (!v) return []; return v.split(',').filter(x => !allow || allow.indexOf(x) !== -1); };
            catalogFilterState.categories = arr('cats', ['men','women','unisex']);
            catalogFilterState.inStockOnly = u.searchParams.get('stock') === '1';
            const s = u.searchParams.get('sort');
            if (s && SORT_LABELS_AR[s]) { catalogFilterState.sort = s; catalogFilterState.sortTouched = true; }
            const pmin = parseInt(u.searchParams.get('pmin') || '', 10);
            const pmax = parseInt(u.searchParams.get('pmax') || '', 10);
            catalogFilterState.priceMin = (!isNaN(pmin) && pmin >= 0) ? pmin : null;
            catalogFilterState.priceMax = (!isNaN(pmax) && pmax >= 0) ? pmax : null;
        } catch (e) {}
    }
    function writeCatalogFiltersToURL() {
        try {
            const u = new URL(window.location.href);
            const setOrDel = (k, v) => { if (v && (Array.isArray(v) ? v.length : true)) u.searchParams.set(k, Array.isArray(v) ? v.join(',') : v); else u.searchParams.delete(k); };
            setOrDel('cats', catalogFilterState.categories.length ? catalogFilterState.categories : null);
            setOrDel('stock', catalogFilterState.inStockOnly ? '1' : null);
            setOrDel('sort', catalogFilterState.sort !== 'newest' ? catalogFilterState.sort : null);
            setOrDel('pmin', catalogFilterState.priceMin != null ? String(catalogFilterState.priceMin) : null);
            setOrDel('pmax', catalogFilterState.priceMax != null ? String(catalogFilterState.priceMax) : null);
            // clear obsolete params from older versions of the filter
            ['vols','fams','occs','rat'].forEach(k => u.searchParams.delete(k));
            history.replaceState(history.state, '', u.toString());
        } catch (e) {}
    }

    // ---- Number helpers (eastern Arabic numerals when RTL) ----
    function toArabicDigits(n) {
        const digits = '٠١٢٣٤٥٦٧٨٩';
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const s = String(n);
        return isRtl ? s.replace(/[0-9]/g, d => digits[+d]) : s;
    }

    // ---- Renderer: sync the simple inline filter bar from state ----
    function renderFilterUI() {
        const panel = document.getElementById('catalogFilterPanel');
        if (!panel) return;
        const st = catalogFilterState;
        const isDefault = st.categories.length === 0 && !st.sortTouched
            && !st.inStockOnly && st.priceMin == null && st.priceMax == null;
        panel.querySelectorAll('.filter-btn').forEach(b => {
            const f = b.dataset.f || '';
            let on = false;
            if (f === 'all') on = isDefault;
            else if (f === 'stock') on = st.inStockOnly;
            else if (f.indexOf('cat:') === 0) on = st.categories.indexOf(f.slice(4)) !== -1;
            else if (f.indexOf('sort:') === 0) on = (st.sortTouched && st.sort === f.slice(5));
            b.classList.toggle('active', on);
        });
    }

    function _resetCatalogFilterState() {
        catalogFilterState.categories = [];
        catalogFilterState.volumes = [];
        catalogFilterState.families = [];
        catalogFilterState.occasions = [];
        catalogFilterState.minRating = 0;
        catalogFilterState.inStockOnly = false;
        catalogFilterState.sort = 'newest';
        catalogFilterState.sortTouched = false;
        catalogFilterState.priceMin = null;
        catalogFilterState.priceMax = null;
    }
    window.clearAllCatalogFilters = function () {
        _resetCatalogFilterState();
        applyCatalogFilters();
    };

    // ---- Apply filters: build query + clientSort, then renderCatalog ----
    let _cfpDebounceTimer = null;
    function applyCatalogFilters(debounced) {
        if (debounced) {
            clearTimeout(_cfpDebounceTimer);
            _cfpDebounceTimer = setTimeout(() => applyCatalogFilters(false), 300);
            renderFilterUI();
            return;
        }
        renderFilterUI();
        writeCatalogFiltersToURL();
        // Backend filtering: only sort goes to the API. Category multi-select + everything else is client-side.
        let q = '';
        let clientSort = null;
        if (catalogFilterState.sort === 'bestseller')      q += '&sortBy=sold&sortDesc=true';
        else if (catalogFilterState.sort === 'newest')     q += '&sortBy=createdAt&sortDesc=true';
        else if (catalogFilterState.sort === 'price-asc')  clientSort = 'price-asc';
        else if (catalogFilterState.sort === 'price-desc') clientSort = 'price-desc';
        renderCatalogWithPriceFilter(q, clientSort);
    }

    // Tests whether a product matches a fragrance-family / occasion keyword set.
    // Searches in name, nameEn, inspiredBy, description, notesTop/Heart/Base — case-insensitive.
    function _productMatchesAnyKeyword(p, keywords) {
        if (!keywords || !keywords.length) return false;
        const hay = [
            p.name || '', p.nameEn || '', p.inspiredBy || '',
            p.description || '', p.notesTop || '', p.notesHeart || '', p.notesBase || ''
        ].join(' ').toLowerCase();
        for (let i = 0; i < keywords.length; i++) {
            if (hay.indexOf(keywords[i].toLowerCase()) !== -1) return true;
        }
        return false;
    }

    async function renderCatalogWithPriceFilter(query, clientSort) {
        const el = document.getElementById('catalogGrid');
        if (!el) return;
        el.innerHTML = skeleton(8);
        const seq = ++catalogReqSeq;
        try {
            const data = await API.fetch('/products?pageSize=100' + (query || ''));
            if (seq !== catalogReqSeq) return;
            let items = data.items || [];
            items.forEach(p => { if (!productMap[p.id]) productMap[p.id] = p; });
            const st = catalogFilterState;

            // 1) Multi-select category: keep products whose category matches any selected.
            //    Server categories are "Men"|"Women"|"Unisex" (Pascal), state values are lowercase.
            if (st.categories.length) {
                const cats = st.categories.map(c => c.toLowerCase());
                items = items.filter(p => cats.indexOf(String(p.category || '').toLowerCase()) !== -1);
            }
            // 2) Volume — keep products that have a size in any selected volume with stock > 0 (or any).
            if (st.volumes.length) {
                items = items.filter(p =>
                    Array.isArray(p.sizes) &&
                    p.sizes.some(s => st.volumes.indexOf(s.volume) !== -1)
                );
            }
            // 3) Fragrance families (multi) — product must match AT LEAST ONE selected family.
            if (st.families.length) {
                items = items.filter(p => st.families.some(fam =>
                    _productMatchesAnyKeyword(p, FAMILY_KEYWORDS[fam])
                ));
            }
            // 4) Occasions (multi) — same logic.
            if (st.occasions.length) {
                items = items.filter(p => st.occasions.some(occ =>
                    _productMatchesAnyKeyword(p, OCCASION_KEYWORDS[occ])
                ));
            }
            // 5) Rating filter.
            if (st.minRating > 0) {
                if (st.minRating === 5) items = items.filter(p => (p.rating || 0) >= 4.9);
                else items = items.filter(p => (p.rating || 0) >= st.minRating);
            }
            // 6) In-stock only.
            if (st.inStockOnly) items = items.filter(p => (p.totalStock || 0) > 0);
            // 7) Price range (free-form min/max; null = unbounded).
            if (st.priceMin != null || st.priceMax != null) {
                items = items.filter(p => {
                    const price = p.minPrice || (p.sizes && p.sizes[0] && p.sizes[0].price) || 0;
                    if (st.priceMin != null && price < st.priceMin) return false;
                    if (st.priceMax != null && price > st.priceMax) return false;
                    return true;
                });
            }
            // 8) Price sort (client-side).
            if (clientSort === 'price-asc') items = items.slice().sort((a, b) => (a.minPrice || 0) - (b.minPrice || 0));
            else if (clientSort === 'price-desc') items = items.slice().sort((a, b) => (b.minPrice || 0) - (a.minPrice || 0));

            if (!items.length) { gridEmpty(el, t('قريبًا', 'Coming soon')); return; }
            // Infinite scroll: اعرض ٨ وحمّل الباقي مع النزول (بدل رسم كل المنتجات دفعة واحدة).
            setupInfiniteGrid(el, items, productCardHTML, IG_BATCH);
        } catch (e) {
            if (seq === catalogReqSeq) gridError(el, e.message, 'renderCatalog');
        }
    }

    // ---- Initial wire-up (after panel exists in DOM) ----
    document.addEventListener('DOMContentLoaded', function () {
        const panel = document.getElementById('catalogFilterPanel');
        if (!panel) return;
        readCatalogFiltersFromURL();

        // Pill buttons — each maps to a category, a sort, the in-stock toggle, or "All" reset.
        panel.querySelectorAll('.filter-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                const f = btn.dataset.f || '';
                const st = catalogFilterState;
                if (f === 'all') {
                    _resetCatalogFilterState(); // يشمل فلاتر التصنيفات المخفية (صيف/مناسبات) من مربعات مجموعاتنا
                } else if (f === 'stock') {
                    st.inStockOnly = !st.inStockOnly;
                } else if (f.indexOf('cat:') === 0) {
                    const c = f.slice(4);
                    // single-select category; clicking the active one clears it
                    st.categories = (st.categories.length === 1 && st.categories[0] === c) ? [] : [c];
                } else if (f.indexOf('sort:') === 0) {
                    const v = f.slice(5);
                    // clicking the active sort again returns to default order
                    if (st.sortTouched && st.sort === v) { st.sort = 'newest'; st.sortTouched = false; }
                    else { st.sort = v; st.sortTouched = true; }
                }
                applyCatalogFilters();
            });
        });

        // Initial render: don't auto-fetch — wait until user navigates to perfumes page.
        renderFilterUI();
    });

    // Re-apply filters whenever the perfumes page is shown
    document.addEventListener('remal:navigated', (e) => {
        if (e && e.detail === 'perfumes') {
            renderFilterUI();
            applyCatalogFilters();
        }
    });

    // ================= PREMIUM SEARCH SYSTEM (Sephora/Apple-class) =================
    // Instant predictive dropdown · fuzzy matching · bilingual · recent/popular · keyboard nav.
    // Falls back gracefully when productMap is empty (early load) by hitting the API.

    let searchTimer = null;
    let currentSearchQuery = '';
    let activeSearchIndex = -1;
    let lastSearchSuggestions = [];
    const SEARCH_RECENT_KEY = 'ramal_search_recent';
    const POPULAR_SEARCHES = [
        { ar: 'عود', en: 'Oud' },
        { ar: 'فريش', en: 'Fresh' },
        { ar: 'حمضيات', en: 'Citrus' },
        { ar: 'كراميل', en: 'Caramel' },
        { ar: 'فانيليا', en: 'Vanilla' },
        { ar: 'صيف', en: 'Summer' },
        { ar: 'شتاء', en: 'Winter' },
        { ar: 'رجالي', en: 'Men' }
    ];

    function getRecentSearches() {
        try { return JSON.parse(localStorage.getItem(SEARCH_RECENT_KEY) || '[]'); } catch (e) { return []; }
    }
    function pushRecentSearch(q) {
        const v = (q || '').trim();
        if (!v) return;
        let list = getRecentSearches().filter(x => x.toLowerCase() !== v.toLowerCase());
        list.unshift(v);
        if (list.length > 6) list = list.slice(0, 6);
        try { localStorage.setItem(SEARCH_RECENT_KEY, JSON.stringify(list)); } catch (e) {}
    }
    function clearRecentSearches() {
        try { localStorage.removeItem(SEARCH_RECENT_KEY); } catch (e) {}
        renderSearchPanel();
    }
    window.clearRecentSearches = clearRecentSearches;

    // Arabic-aware normalization for fuzzy matching: strip diacritics, unify alif/yaa/taa-marbuta forms.
    function normAr(s) {
        return String(s || '')
            .toLowerCase()
            .replace(/[ً-ْٰ]/g, '')  // tashkeel
            .replace(/[أإآ]/g, 'ا') // أ إ آ → ا
            .replace(/ى/g, 'ي') // ى → ي
            .replace(/ة/g, 'ه') // ة → ه
            .replace(/[^؀-ۿa-z0-9\s]/g, ' ')
            .replace(/\s+/g, ' ')
            .trim();
    }

    // Score a product against a normalized query. Higher = better match. Returns 0 if no match.
    function scoreProductForSearch(p, qNorm) {
        if (!qNorm) return 0;
        const tokens = qNorm.split(' ').filter(Boolean);
        if (!tokens.length) return 0;
        const haystacks = [
            { text: normAr(p.name), w: 10 },
            { text: normAr(p.nameEn), w: 9 },
            { text: normAr(p.inspiredBy), w: 6 },
            { text: normAr(p.notesTop), w: 4 },
            { text: normAr(p.notesHeart), w: 4 },
            { text: normAr(p.notesBase), w: 4 },
            { text: normAr(p.category), w: 3 },
        ];
        let total = 0;
        let allTokensMatched = true;
        for (const tok of tokens) {
            let tokenScore = 0;
            for (const h of haystacks) {
                if (!h.text) continue;
                const idx = h.text.indexOf(tok);
                if (idx === -1) continue;
                // exact word boundary match scores higher than substring
                const boundary = (idx === 0 || h.text[idx - 1] === ' ') ? 1.5 : 1;
                const lengthRatio = tok.length / Math.max(h.text.length, 1);
                tokenScore = Math.max(tokenScore, h.w * boundary * (0.5 + lengthRatio));
            }
            if (tokenScore === 0) { allTokensMatched = false; }
            total += tokenScore;
        }
        if (!allTokensMatched) total *= 0.4; // demote partial matches
        // Bestseller / sold boost
        if (p.sold) total += Math.min(p.sold / 50, 3);
        // In-stock boost; out-of-stock gets demoted but not zeroed
        if (p.totalStock === 0) total *= 0.7;
        return total;
    }

    function highlightMatch(text, qNorm) {
        if (!text) return '';
        const escaped = esc(text);
        if (!qNorm) return escaped;
        const tokens = qNorm.split(' ').filter(t => t.length >= 2);
        if (!tokens.length) return escaped;
        // Use a single pass — wrap each token where it appears (case-insensitive, Arabic-norm aware)
        const normText = normAr(text);
        const positions = [];
        for (const tok of tokens) {
            let i = 0;
            while (i <= normText.length - tok.length) {
                const idx = normText.indexOf(tok, i);
                if (idx === -1) break;
                positions.push([idx, idx + tok.length]);
                i = idx + tok.length;
            }
        }
        if (!positions.length) return escaped;
        positions.sort((a, b) => a[0] - b[0]);
        // Merge overlaps
        const merged = [positions[0]];
        for (let k = 1; k < positions.length; k++) {
            const last = merged[merged.length - 1];
            if (positions[k][0] <= last[1]) last[1] = Math.max(last[1], positions[k][1]);
            else merged.push(positions[k]);
        }
        // Build highlighted html — index against normText is roughly aligned with original (we only stripped diacritics)
        let out = '';
        let cursor = 0;
        for (const [s, e] of merged) {
            out += esc(text.slice(cursor, s)) + '<mark>' + esc(text.slice(s, e)) + '</mark>';
            cursor = e;
        }
        out += esc(text.slice(cursor));
        return out;
    }

    function searchProducts(query, max) {
        max = max || 7;
        const qNorm = normAr(query);
        const pool = allProducts.length ? allProducts : Object.values(productMap);
        const scored = [];
        for (const p of pool) {
            const s = scoreProductForSearch(p, qNorm);
            if (s > 0) scored.push({ p, s });
        }
        scored.sort((a, b) => b.s - a.s);
        return scored.slice(0, max).map(x => ({ ...x.p, _score: x.s }));
    }

    function categoryLabel(cat) {
        const map = { Men: t('رجالي', 'For Him'), Women: t('نسائي', 'For Her'), Unisex: t('للجنسين', 'Unisex') };
        return map[cat] || cat || '';
    }

    function suggestionCardHTML(p, qNorm) {
        const isRtl = document.documentElement.dir === 'rtl';
        const display = isRtl ? (p.name || '') : (p.nameEn || p.name || '');
        const isBest = (p.sold || 0) >= 30;
        return ''
            + '<div class="search-suggestion" role="option" data-product-id="' + esc(p.id) + '" tabindex="-1">'
            +   '<img src="' + esc(imgUrl(p.imageUrl, 200)) + '" alt="' + esc(display) + '" loading="lazy" decoding="async">'
            +   '<div class="info">'
            +     '<div class="nm">' + highlightMatch(display, qNorm) + '</div>'
            +     '<div class="meta">'
            +       '<span class="cat">' + esc(categoryLabel(p.category)) + '</span>'
            +       (isBest ? '<span class="badge-best">★ ' + t('الأكثر مبيعاً', 'Bestseller') + '</span>' : '')
            +       (p.inspiredBy ? '<span>' + esc(t('مستوحى من: ', 'Inspired by: ') + p.inspiredBy) + '</span>' : '')
            +     '</div>'
            +   '</div>'
            +   '<div class="price">'
            +     '<span class="en-num">' + Number(p.minPrice || 0).toLocaleString('en-US') + '</span>'
            +     '<small>' + t('ج.م', 'EGP') + '</small>'
            +   '</div>'
            + '</div>';
    }

    function chipsHTML(items, kind) {
        return '<div class="search-chip-row">'
            + items.map(item => {
                const label = typeof item === 'string' ? item : (document.documentElement.dir === 'rtl' ? item.ar : item.en);
                const icon = kind === 'recent'
                    ? '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>'
                    : '<svg viewBox="0 0 24 24"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>';
                return '<button type="button" class="search-chip" data-chip="' + esc(label) + '">'
                    + icon + '<span>' + esc(label) + '</span></button>';
            }).join('')
            + '</div>';
    }

    function renderSearchPanel() {
        const panel = document.getElementById('searchResultsPanel');
        if (!panel) return;
        const q = currentSearchQuery;
        const qNorm = normAr(q);
        activeSearchIndex = -1;

        // === Empty state — no query typed yet ===
        if (!q) {
            const recent = getRecentSearches();
            const popular = POPULAR_SEARCHES;
            // Show top-selling products as visual inspiration too
            const inspiration = (allProducts.slice().sort((a, b) => (b.sold || 0) - (a.sold || 0)).slice(0, 4));
            lastSearchSuggestions = inspiration.slice();
            let html = '';
            if (recent.length) {
                html += '<div class="search-section">'
                    + '<div class="search-section-title"><span>' + t('بحوثاتك الأخيرة', 'Recent searches') + '</span>'
                    +   '<button type="button" onclick="clearRecentSearches()">' + t('مسح', 'Clear') + '</button></div>'
                    + chipsHTML(recent, 'recent') + '</div>';
            }
            html += '<div class="search-section">'
                + '<div class="search-section-title"><span>' + t('الأكثر بحثاً', 'Trending') + '</span></div>'
                + chipsHTML(popular, 'popular') + '</div>';
            if (inspiration.length) {
                html += '<div class="search-section">'
                    + '<div class="search-section-title"><span>' + t('اقتراحات لك', 'You may like') + '</span></div>'
                    + inspiration.map(p => suggestionCardHTML(p, '')).join('')
                    + '</div>';
            }
            panel.innerHTML = html;
            return;
        }

        // === Live results ===
        const results = searchProducts(q, 7);
        lastSearchSuggestions = results;
        if (!results.length) {
            // No-result state — still show inspirations + popular chips
            const inspiration = (allProducts.slice().sort((a, b) => (b.sold || 0) - (a.sold || 0)).slice(0, 4));
            lastSearchSuggestions = inspiration.slice();
            panel.innerHTML = ''
                + '<div class="search-empty">'
                +   '<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/><line x1="14" y1="8" x2="8" y2="14"/><line x1="8" y1="8" x2="14" y2="14"/></svg>'
                +   '<div class="title">' + t('مفيش نتيجة لـ "' + esc(q) + '"', 'No matches for "' + esc(q) + '"') + '</div>'
                +   '<div class="sub">' + t('جرب كلمة تانية أو شوف اقتراحاتنا تحت', 'Try a different term or check suggestions below') + '</div>'
                + '</div>'
                + (inspiration.length ? '<div class="search-section">'
                    + '<div class="search-section-title"><span>' + t('الأكثر طلباً', 'Popular right now') + '</span></div>'
                    + inspiration.map(p => suggestionCardHTML(p, '')).join('')
                    + '</div>' : '')
                + '<div class="search-section">'
                +   chipsHTML(POPULAR_SEARCHES, 'popular')
                + '</div>';
            return;
        }
        panel.innerHTML = ''
            + '<div class="search-section">'
            +   '<div class="search-section-title"><span>' + t('نتائج البحث', 'Suggestions') + '</span><span class="en-num">' + results.length + '</span></div>'
            +   results.map(p => suggestionCardHTML(p, qNorm)).join('')
            + '</div>'
            + '<button type="button" class="search-view-all" data-view-all>'
            +   t('عرض كل النتائج لـ "', 'View all results for "') + esc(q) + '"'
            +   '<svg viewBox="0 0 24 24" style="transform:' + (document.documentElement.dir === 'rtl' ? 'rotate(180deg)' : 'none') + '"><path d="M5 12h14M12 5l7 7-7 7"/></svg>'
            + '</button>';
    }

    function moveSearchSelection(delta) {
        const items = Array.from(document.querySelectorAll('#searchResultsPanel .search-suggestion'));
        if (!items.length) return;
        items.forEach(it => it.classList.remove('active'));
        activeSearchIndex = ((activeSearchIndex + delta) % items.length + items.length) % items.length;
        items[activeSearchIndex].classList.add('active');
        items[activeSearchIndex].scrollIntoView({ block: 'nearest' });
    }

    function commitSearch(q) {
        const v = (q == null ? currentSearchQuery : q).trim();
        if (!v) return;
        pushRecentSearch(v);
        const modal = document.getElementById('searchModal');
        if (modal && modal.classList.contains('active') && typeof toggleSearch === 'function') toggleSearch();
        navigate('perfumes');
        const input = document.getElementById('searchInput');
        if (input) input.value = v;
        currentSearchQuery = v;
        const cg = document.getElementById('catalogGrid');
        if (cg) cg.dataset.loaded = '1';
        renderCatalog('&search=' + encodeURIComponent(v));
    }

    function openSearchProduct(productId) {
        pushRecentSearch(currentSearchQuery || '');
        const modal = document.getElementById('searchModal');
        if (modal && modal.classList.contains('active') && typeof toggleSearch === 'function') toggleSearch();
        currentProductId = productId;
        navigate('product-detail');
        const p = productMap[productId];
        if (p && typeof renderProductDetail === 'function') renderProductDetail(p);
    }

    // Exposed so the navigate() wrapper can clear state on nav-away.
    window.clearSearchState = function () {
        currentSearchQuery = '';
        const input = document.getElementById('searchInput');
        if (input) input.value = '';
        const clearBtn = document.getElementById('searchClearBtn');
        if (clearBtn) clearBtn.style.display = 'none';
        const cg = document.getElementById('catalogGrid');
        if (cg) cg.dataset.loaded = '0';
        clearTimeout(searchTimer);
    };

    function wireSearch() {
        const input = document.getElementById('searchInput');
        const panel = document.getElementById('searchResultsPanel');
        const clearBtn = document.getElementById('searchClearBtn');
        const modal = document.getElementById('searchModal');
        if (!input || input.dataset.wired === '1') return;
        input.dataset.wired = '1';

        // Ensure productMap is warm before live search; if empty, kick off load (no-blocking).
        if (!allProducts.length) {
            loadProductsBase().then(() => { if (modal && modal.classList.contains('active')) renderSearchPanel(); }).catch(() => {});
        }

        input.addEventListener('input', function () {
            clearTimeout(searchTimer);
            currentSearchQuery = input.value;
            if (clearBtn) clearBtn.style.display = input.value ? 'inline-flex' : 'none';
            // 220ms debounce — feels live but doesn't hammer
            searchTimer = setTimeout(() => {
                renderSearchPanel();
                // تتبع: بحث — نية عالية، وبيتبعت مرة واحدة لكل كلمة بحث مكتملة
                const _q = (input.value || '').trim();
                if (_q.length >= 3 && _q !== window.__lastTrackedSearch) {
                    window.__lastTrackedSearch = _q;
                    try { window.RemalTrack.event('search', { searchTerm: _q }); } catch (e) {}
                }
            }, 220);
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); moveSearchSelection(+1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); moveSearchSelection(-1); }
            else if (e.key === 'Enter') {
                e.preventDefault();
                const items = Array.from(document.querySelectorAll('#searchResultsPanel .search-suggestion'));
                if (activeSearchIndex >= 0 && items[activeSearchIndex]) {
                    openSearchProduct(items[activeSearchIndex].getAttribute('data-product-id'));
                } else {
                    commitSearch(input.value);
                }
            }
            else if (e.key === 'Escape') { e.preventDefault(); if (typeof toggleSearch === 'function') toggleSearch(); }
        });

        if (clearBtn) clearBtn.addEventListener('click', function () {
            input.value = '';
            currentSearchQuery = '';
            clearBtn.style.display = 'none';
            renderSearchPanel();
            input.focus();
        });

        // Event delegation on the panel: suggestion click, chip click, view-all click
        if (panel && !panel.dataset.wired) {
            panel.dataset.wired = '1';
            panel.addEventListener('click', function (e) {
                const sug = e.target.closest('.search-suggestion');
                if (sug) { openSearchProduct(sug.getAttribute('data-product-id')); return; }
                const chip = e.target.closest('.search-chip');
                if (chip) {
                    const term = chip.getAttribute('data-chip') || chip.textContent.trim();
                    input.value = term;
                    currentSearchQuery = term;
                    if (clearBtn) clearBtn.style.display = 'inline-flex';
                    renderSearchPanel();
                    input.focus();
                    return;
                }
                if (e.target.closest('[data-view-all]')) commitSearch(input.value);
            });
        }

        // Render initial panel state when modal opens — observe via toggle hook
        const origToggle = window.toggleSearch;
        if (typeof origToggle === 'function' && !origToggle._wrapped) {
            window.toggleSearch = function () {
                origToggle();
                if (modal && modal.classList.contains('active')) {
                    // Warm up products if not loaded
                    if (!allProducts.length) loadProductsBase().then(renderSearchPanel).catch(() => renderSearchPanel());
                    else renderSearchPanel();
                    setTimeout(() => input.focus(), 80);
                } else {
                    activeSearchIndex = -1;
                }
            };
            window.toggleSearch._wrapped = true;
        }
    }

    // ---- product detail ----
    // ===== تصفير صفحات التفاصيل قبل عرضها =====
    // navigate() بتعرض القسم فورًا، لكن بيانات المنتج بتوصل بعد رحلة شبكة كاملة.
    // في الفترة دي كان الـ DOM لسه شايل بيانات المنتج اللي اتفتح قبل كده (أو القيم
    // الثابتة في الماركب) — فالعميل يشوف سعر ووصف وحالة مخزون بتاعة منتج تاني خالص.
    // الحل: نفضّي الحقول *بشكل متزامن* قبل navigate — فمفيش لحظة واحدة ببيانات غلط.
    const _BLANK_PX = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';
    function resetDetailView(sectionId, opts) {
        const sec = document.getElementById(sectionId);
        if (!sec) return;
        const o = opts || {};
        // النصوص: نمسح المحتوى وبدائل اللغة معًا (وإلا toggleLanguage يرجّع القديم)
        (o.textSelectors || []).forEach(sel => {
            sec.querySelectorAll(sel).forEach(el => {
                el.innerHTML = '';
                if (el.hasAttribute('data-ar')) el.setAttribute('data-ar', '');
                if (el.hasAttribute('data-en')) el.setAttribute('data-en', '');
            });
        });
        (o.emptyIds || []).forEach(id => { const el = document.getElementById(id); if (el) el.textContent = ''; });
        (o.hideIds || []).forEach(id => { const el = document.getElementById(id); if (el) el.hidden = true; });
        // حالة المخزون: تختفي ويتفضّى نصها لحد ما نعرف الرقم الحقيقي
        const row = sec.querySelector('.pd-stock-row');
        if (row) { row.hidden = true; row.classList.remove('pd-stock--in', 'pd-stock--low', 'pd-stock--out'); }
        const stockTxt = sec.querySelector('.pd-stock-text');
        if (stockTxt) { stockTxt.textContent = ''; stockTxt.setAttribute('data-ar', ''); stockTxt.setAttribute('data-en', ''); }
        // الصور: بكسل شفاف بدل صورة المنتج السابق
        (o.imageSelectors || []).forEach(sel => {
            sec.querySelectorAll(sel).forEach(img => { img.src = _BLANK_PX; img.alt = ''; img.style.filter = ''; });
        });
        (o.clearHtmlIds || []).forEach(id => { const el = document.getElementById(id); if (el) el.innerHTML = ''; });
    }
    window.resetDetailView = resetDetailView;
    function resetProductDetailView() {
        resetDetailView('product-detail', {
            textSelectors: ['h1', '.insp-text', '.p-desc', '.accordion-content-inner:not(#pdPerformanceText)'],
            emptyIds: ['mainPriceDisplay', 'btnPriceDisplay', 'stickyProductName', 'stickyPriceDisplay'],
            hideIds: ['pdOldPrice', 'pdSavePill'],
            imageSelectors: ['#productSlider img', '#sliderThumbs img', '#stickyProductImg'],
            clearHtmlIds: ['pdSizeGrid', 'pdReviewsList']
        });
        const bc = document.querySelector('#product-detail .breadcrumb');
        if (bc) { const v = t('الرئيسية / عطورنا', 'Home / Perfumes'); bc.textContent = v; bc.setAttribute('data-ar', 'الرئيسية / عطورنا'); bc.setAttribute('data-en', 'Home / Perfumes'); }
        const rg = document.getElementById('relatedGrid');
        if (rg && typeof skeleton === 'function') rg.innerHTML = skeleton(4);
    }
    window.openProductDetail = async function (id) {
        currentProductId = id;
        resetProductDetailView();          // ← متزامن، قبل أي عرض أو انتظار شبكة
        navigate('product-detail');
        try {
            const p = await API.fetch('/products/' + id);
            if (currentProductId !== id) return;   // العميل فتح منتجًا آخر أثناء الانتظار
            productMap[id] = p;
            renderProductDetail(p);
            API.fetch('/products/' + id + '/related?take=4')
                .then(ids => { if (currentProductId === id) renderRelatedProducts('relatedGrid', ids); }).catch(() => {});
            API.fetch('/reviews/by-product/' + id)
                .then(rv => { if (currentProductId === id) renderReviews('pdReviewsList', rv); }).catch(() => {});
        } catch (e) { toastMsg(e.message); }
    };
    function renderProductDetail(p) {
        const sec = document.getElementById('product-detail');
        const set = (sel, html) => { const el = sec.querySelector(sel); if (el) el.innerHTML = html; };
        set('.breadcrumb', t('الرئيسية / عطورنا / ', 'Home / Perfumes / ') + esc(pname(p)));
        set('h1', esc(pname(p)));
        set('.insp-text', pinspired(p) ? (t('مستوحى من: ', 'Inspired by: ') + esc(pinspired(p))) : '');
        const ds = defaultSize(p);
        const stockEl = sec.querySelector('.pd-stock-text');
        const stockRow = sec.querySelector('.pd-stock-row');
        if (stockRow) stockRow.hidden = false;   // مخفي في الماركب لحد ما نعرف المخزون الحقيقي
        if (stockEl) {
            const total = p.totalStock || 0;
            let status = 'in', label;
            if (total <= 0)      { status = 'out'; label = t('غير متوفر',  'Out of Stock'); }
            else if (total <= 5) { status = 'low'; label = t('كميات محدودة', 'Low Stock'); }
            else                 { status = 'in';  label = t('متاح',         'In Stock'); }
            stockEl.textContent = label;
            // لازم نحدّث بدائل اللغة كمان — العنصر .lang-text وتبديل اللغة بيقرأ منها،
            // ولو سابناها فاضية (زي الماركب) الحالة كانت هتختفي عند تبديل اللغة.
            stockEl.setAttribute('data-ar', label); stockEl.setAttribute('data-en', label);
            if (stockRow) {
                stockRow.classList.remove('pd-stock--in', 'pd-stock--low', 'pd-stock--out');
                stockRow.classList.add('pd-stock--' + status);
            }
        }
        const priceEl = document.getElementById('mainPriceDisplay');
        if (priceEl) priceEl.textContent = money(ds.price);
        set('.p-desc', esc(pdesc(p)));
        // "الأداء والثبات" — نص خاص بكل عطر من لوحة التحكم (يسقط للنص الافتراضي عند غيابه)
        const perfEl = document.getElementById('pdPerformanceText');
        if (perfEl) {
            if (!perfEl.dataset.defAr) { perfEl.dataset.defAr = perfEl.getAttribute('data-ar') || ''; perfEl.dataset.defEn = perfEl.getAttribute('data-en') || ''; }
            const perfAr = (p.performanceAr || '').trim() || perfEl.dataset.defAr;
            const perfEn = (p.performanceEn || '').trim() || (p.performanceAr || '').trim() || perfEl.dataset.defEn;
            perfEl.setAttribute('data-ar', perfAr);
            perfEl.setAttribute('data-en', perfEn);
            perfEl.textContent = isRtl() ? perfAr : perfEn;
        }
        // تتبع: مشاهدة منتج — أساس جمهور "شاف ومشتراش"
        try {
            const _s = defaultSize(p);
            window.RemalTrack.event('view_item', {
                value: _s.price,
                items: [{ id: p.id, name: p.nameEn || p.name, category: p.category,
                          variant: _s.volume, price: _s.price, quantity: 1 }]
            });
        } catch (e) {}
        // sizes — out-of-stock sizes are visible but disabled, the rest is auto-selected sanely
        const grid = document.getElementById('pdSizeGrid');
        let activePriceFromGrid = ds.price;
        if (grid) {
            const ss = sortedSizes(p).slice().reverse(); // 100,50,30
            const inStock = ss.filter(s => s.stock > 0);
            // Auto-select target: keep the default (50ML or first) if it has stock; otherwise pick the first in-stock size.
            // If exactly ONE size is in stock, force-select it.
            const autoTarget = inStock.length === 1
                ? inStock[0]
                : (ds && ds.stock > 0 ? ds : inStock[0]);
            grid.innerHTML = ss.map(s => {
                const out = s.stock <= 0;
                const isActive = !out && autoTarget && s.volume === autoTarget.volume;
                const title = out ? t('نفدت الكمية', 'Sold out') : t('متاح', 'Available');
                const cls = 'size-btn en-num' + (out ? ' out-of-stock' : '') + (isActive ? ' active' : '');
                const attrs = 'data-volume="' + s.volume + '" data-stock="' + s.stock + '" data-price="' + s.price + '"'
                    + (hasDiscount(s) ? ' data-oldprice="' + s.oldPrice + '"' : '')
                    + ' title="' + title + '"'
                    + (out ? ' disabled aria-disabled="true"' : '')
                    + (out ? '' : ' onclick="selectSize(this,' + s.price + ')"');
                const label = dispVol(s.volume);
                const inner = out
                    ? '<span class="oos-vol">' + label + '</span><span class="oos-label">' + t('نفدت الكمية', 'Sold out') + '</span>'
                    : label;
                return '<button class="' + cls + '" ' + attrs + '>' + inner + '</button>';
            }).join('');
            if (autoTarget) activePriceFromGrid = autoTarget.price;
        }
        basePrice = activePriceFromGrid;
        currentQty = 1;
        const qv = document.getElementById('qtyValue'); if (qv) qv.textContent = '1';
        // شطب السعر القديم للحجم المختار تلقائيًا عند فتح المنتج
        if (typeof syncOldPriceDisplay === 'function') syncOldPriceDisplay(null);
        if (typeof updateTotalDisplay === 'function') updateTotalDisplay();
        // slider images — use up to 3 distinct images, hide empty slots
        const galleryRaw = [p.imageUrl, p.imageUrl2, p.imageUrl3].filter(Boolean);
        const gallery = galleryRaw.map(function (x) { return imgUrl(x, 800); });
        const GALLERY_SIZES = '(max-width: 900px) 92vw, 700px';
        if (gallery.length === 0) gallery.push('');
        const slides = sec.querySelectorAll('#productSlider .product-slide');
        slides.forEach((slide, i) => {
            const img = slide.querySelector('img');
            if (i < gallery.length) {
                slide.style.display = '';
                if (img) {
                    img.src = gallery[i];
                    img.srcset = imgSrcset(galleryRaw[i], [800, 1200, 1600]);
                    img.sizes = GALLERY_SIZES;
                    img.decoding = 'async';
                    img.alt = p.name; img.style.filter = '';
                }
            } else {
                slide.style.display = 'none';
            }
        });
        const thumbs = sec.querySelectorAll('#sliderThumbs .slider-thumb');
        thumbs.forEach((thumb, i) => {
            const img = thumb.querySelector('img');
            if (i < gallery.length) {
                thumb.style.display = '';
                if (img) {
                    // المصغّرة معروضة ~٤٥ بكسل — ٢٠٠ تكفيها حتى على شاشة ٣×
                    img.src = imgUrl(galleryRaw[i], 200);
                    img.srcset = ''; img.sizes = '';
                    img.style.filter = ''; img.alt = p.name;
                }
                thumb.classList.toggle('active', i === 0);
                thumb.onclick = function () { goToSlide(i); };
            } else {
                thumb.style.display = 'none';
            }
        });
        // notes accordion (first inner)
        const notesInner = sec.querySelector('.accordion-content-inner');
        if (notesInner) {
            notesInner.innerHTML = '<strong>' + t('المقدمة:', 'Top:') + '</strong> ' + esc(pnote(p, 'Top') || '—') + '<br><br>'
                + '<strong>' + t('القلب:', 'Heart:') + '</strong> ' + esc(pnote(p, 'Heart') || '—') + '<br><br>'
                + '<strong>' + t('القاعدة:', 'Base:') + '</strong> ' + esc(pnote(p, 'Base') || '—');
        }
        // wire add + buy-now
        const addBtn = sec.querySelector('#mainActionRow .btn-add-anim');
        if (addBtn) addBtn.onclick = function () { pdAddToCart(addBtn); };
        const buyBtn = sec.querySelector('.btn-buy-now');
        if (buyBtn) buyBtn.onclick = function () { pdAddToCart(null); navigate('checkout'); };
        // sticky bar
        const sImg = document.getElementById('stickyProductImg'); if (sImg) sImg.src = imgUrl(p.imageUrl, 200);
        const sName = document.getElementById('stickyProductName'); if (sName) sName.textContent = p.name;
        const sPrice = document.getElementById('stickyPriceDisplay'); if (sPrice) sPrice.textContent = money(ds.price);
        injectProductJsonLd(p);
    }
    function injectProductJsonLd(p) {
        let el = document.getElementById('productJsonLd');
        if (!el) { el = document.createElement('script'); el.type = 'application/ld+json'; el.id = 'productJsonLd'; document.head.appendChild(el); }
        const base = 'https://remalfragrances.com';
        const ld = {
            '@context': 'https://schema.org', '@type': 'Product',
            name: p.name, image: p.imageUrl || undefined, description: p.description || undefined,
            url: base + '/product/' + p.id,
            brand: { '@type': 'Brand', name: 'Remal Fragrances' },
            offers: {
                '@type': 'AggregateOffer', priceCurrency: 'EGP',
                lowPrice: p.minPrice, highPrice: p.maxPrice,
                availability: (p.totalStock > 0) ? 'https://schema.org/InStock' : 'https://schema.org/OutOfStock'
            }
        };
        // Breadcrumbs — تساعد جوجل يفهم تسلسل الموقع (الرئيسية ← العطور ← العطر)
        let bc = document.getElementById('breadcrumbJsonLd');
        if (!bc) { bc = document.createElement('script'); bc.type = 'application/ld+json'; bc.id = 'breadcrumbJsonLd'; document.head.appendChild(bc); }
        bc.textContent = JSON.stringify({
            '@context': 'https://schema.org', '@type': 'BreadcrumbList',
            itemListElement: [
                { '@type': 'ListItem', position: 1, name: 'الرئيسية', item: base + '/' },
                { '@type': 'ListItem', position: 2, name: 'عطورنا', item: base + '/perfumes' },
                { '@type': 'ListItem', position: 3, name: p.name, item: base + '/product/' + p.id }
            ]
        });
        if (p.rating && p.reviewCount) {
            ld.aggregateRating = { '@type': 'AggregateRating', ratingValue: p.rating, reviewCount: p.reviewCount };
        }
        el.textContent = JSON.stringify(ld);
    }
    function pdAddToCart(btn) {
        const p = productMap[currentProductId];
        if (!p) return;
        const activeSize = document.querySelector('#pdSizeGrid .size-btn.active:not(.out-of-stock)');
        if (!activeSize) {
            // Either no size is selected or the active one became out-of-stock
            toastMsg(t('اختار حجم متاح أولاً', 'Pick an available size first'));
            return;
        }
        const stock = parseInt(activeSize.getAttribute('data-stock') || '0', 10);
        if (stock <= 0) {
            toastMsg(t('نفدت الكمية لهذا الحجم', 'This size is sold out'));
            return;
        }
        const volume = activeSize.getAttribute('data-volume');
        const qty = currentQty || 1;
        if (qty > stock) {
            toastMsg(t('المتاح فقط ' + stock + ' قطعة', 'Only ' + stock + ' left in stock'));
            return;
        }
        addProductToCart({
            productId: currentProductId, volume: volume, price: basePrice || defaultSize(p).price,
            name: p.name, nameEn: p.nameEn, img: p.imageUrl, qty: qty
        });
        if (btn) animateAddBtn(btn);
    }
    window.stickyAddToCart = function () {
        const btn = document.getElementById('stickyAddBtn');
        const p = productMap[currentProductId];
        if (!p) return;
        const activeSize = document.querySelector('#pdSizeGrid .size-btn.active');
        const volume = activeSize ? activeSize.getAttribute('data-volume') : defaultSize(p).volume;
        const sQty = parseInt((document.getElementById('stickyQtyVal') || {}).innerText || '1') || 1;
        addProductToCart({ productId: currentProductId, volume: volume, price: basePrice || defaultSize(p).price, name: p.name, nameEn: p.nameEn, img: p.imageUrl, qty: sQty });
        if (btn) { btn.classList.add('success'); setTimeout(() => btn.classList.remove('success'), 1600); }
    };
    function renderRelatedProducts(gridId, ids) {
        const items = (ids || []).map(id => productMap[id]).filter(Boolean);
        if (items.length) { fillGrid(gridId, items, productCardHTML); }
        else {
            // fall back to fetching detail for unknown ids
            Promise.all((ids || []).slice(0, 4).map(id => API.fetch('/products/' + id).catch(() => null)))
                .then(list => fillGrid(gridId, list.filter(Boolean), productCardHTML));
        }
    }

    // ---- reviews ----
    function stars(n) { return '★'.repeat(n) + '☆'.repeat(5 - n); }
    function reviewCardHTML(r) {
        const d = new Date(r.createdAt);
        const ds = String(d.getDate()).padStart(2, '0') + '/' + String(d.getMonth() + 1).padStart(2, '0') + '/' + d.getFullYear();
        return '<div class="review-card"><div class="review-header"><div class="review-name"><span>' + esc(r.customerName) + '</span>'
            + (r.isVerifiedPurchase ? '<span class="verified-badge">✔</span>' : '') + '</div>'
            + '<div class="review-date en-num">' + ds + '</div></div>'
            + '<div class="review-stars">' + stars(r.rating) + '</div>'
            + '<div class="review-text">' + esc(r.text || '') + '</div></div>';
    }
    function renderReviews(containerId, reviews) {
        const el = document.getElementById(containerId);
        if (!el) return;
        if (!reviews || !reviews.length) {
            el.innerHTML = '<div style="padding:20px 0;color:var(--text-muted);font-size:14px;">' + t('لا توجد تقييمات بعد — كن أول من يشاركنا رأيه', 'No reviews yet — be the first!') + '</div>';
            return;
        }
        el.innerHTML = reviews.map(reviewCardHTML).join('');
    }
    window.injectReviewForms = function () {
        document.querySelectorAll('.add-review-wrap').forEach(wrap => {
            if (wrap.dataset.injected === '1') return;
            wrap.dataset.injected = '1';
            // مهم: النصوص هنا لازم تتكتب كـ data-ar/data-en (وclass="lang-text") مش نص جاهز
            // من t()، لأن الفورم ده بيتحقن مرة واحدة بس (حارس injected) فلو ثبّتنا اللغة
            // وقت الحقن هيفضل بلغتها للأبد ومش هيتبدّل مع زرار اللغة.
            wrap.innerHTML = '<div class="add-review-box">'
                + '<div class="add-review-head">'
                +   '<h4 class="lang-text" data-ar="شاركنا تجربتك" data-en="Share your experience">' + t('شاركنا تجربتك', 'Share your experience') + '</h4>'
                +   '<p class="lang-text" data-ar="قيّم المنتج بعد ما تشتريه." data-en="Rate the product after purchase.">' + t('قيّم المنتج بعد ما تشتريه.', 'Rate the product after purchase.') + '</p>'
                + '</div>'
                + '<input type="text" class="rev-name" data-placeholder-ar="اسمك" data-placeholder-en="Your name" placeholder="' + t('اسمك', 'Your name') + '" style="width:100%;padding:10px;border:1px solid var(--border-color);border-radius:var(--radius);margin-bottom:10px;font-family:inherit;">'
                + '<div class="rate-stars" data-rating="0"><span class="rs" data-v="1">★</span><span class="rs" data-v="2">★</span><span class="rs" data-v="3">★</span><span class="rs" data-v="4">★</span><span class="rs" data-v="5">★</span></div>'
                + '<textarea class="rev-textarea" rows="3" data-placeholder-ar="اكتب تجربتك (اختياري)..." data-placeholder-en="Your experience (optional)..." placeholder="' + t('اكتب تجربتك (اختياري)...', 'Your experience (optional)...') + '"></textarea>'
                + '<button type="button" class="rev-submit lang-text" data-ar="انشر التقييم" data-en="POST REVIEW">' + t('انشر التقييم', 'POST REVIEW') + '</button>'
                + '<div class="rev-msg"></div></div>';
            const starsEls = wrap.querySelectorAll('.rs');
            starsEls.forEach(s => s.addEventListener('click', () => {
                const v = parseInt(s.dataset.v);
                wrap.querySelector('.rate-stars').dataset.rating = v;
                starsEls.forEach(x => x.classList.toggle('active', parseInt(x.dataset.v) <= v));
            }));
            wrap.querySelector('.rev-submit').addEventListener('click', async () => {
                const rating = parseInt(wrap.querySelector('.rate-stars').dataset.rating);
                const text = wrap.querySelector('.rev-textarea').value.trim();
                const name = wrap.querySelector('.rev-name').value.trim();
                const msg = wrap.querySelector('.rev-msg');
                const detail = wrap.getAttribute('data-detail');
                if (detail !== 'product') {
                    msg.innerHTML = '<span style="color:var(--text-muted);">' + t('التقييمات بتتسجل على مستوى المنتج — افتح صفحة العطر.', 'Reviews are submitted per product.') + '</span>';
                    return;
                }
                if (!name) { msg.innerHTML = '<span style="color:var(--red);">' + t('اكتب اسمك', 'Enter your name') + '</span>'; return; }
                if (!rating) { msg.innerHTML = '<span style="color:var(--red);">' + t('اختار عدد النجوم', 'Pick a star rating') + '</span>'; return; }
                if (!currentProductId) { msg.innerHTML = '<span style="color:var(--red);">' + t('افتح صفحة العطر الأول', 'Open a product page first') + '</span>'; return; }
                try {
                    await API.fetch('/reviews', { method: 'POST', noAuth: true, body: { productId: currentProductId, customerName: name, rating: rating, text: text } });
                    msg.innerHTML = '<span style="color:var(--green);">' + t('✓ اتبعت تقييمك للمراجعة، شكراً!', '✓ Review submitted for moderation. Thanks!') + '</span>';
                    wrap.querySelector('.rev-textarea').value = '';
                    wrap.querySelector('.rev-name').value = '';
                    wrap.querySelectorAll('.rs').forEach(x => x.classList.remove('active'));
                    wrap.querySelector('.rate-stars').dataset.rating = '0';
                } catch (e) {
                    msg.innerHTML = '<span style="color:var(--red);">' + esc(e.message) + '</span>';
                }
            });
        });
    };

    // ---- collection detail ----
    window.openCollectionDetail = async function (id) {
        currentCollectionId = id;              // لازم قبل navigate عشان يتحفظ في sessionStorage للريفريش
        resetDetailView('collection-detail', {
            textSelectors: ['h1', '.insp-text', '.p-desc'],
            emptyIds: ['cdPriceDisplay', 'cdOriginalPrice', 'cdSavingsLabel'],
            hideIds: ['cdOriginalPrice', 'cdSavingsWrap'],
            imageSelectors: ['#cdSlider img', '#cdSliderThumbs img'],
            clearHtmlIds: ['cdFragrancesGrid', 'cdReviewsList']
        });
        navigate('collection-detail');
        try {
            const c = await API.fetch('/collections/' + id);
            if (currentCollectionId !== id) return;
            currentCollection = c;
            renderCollectionDetail(c);
            renderCollectionsInto([]); // ensure cache
            if (!collectionsCache) { try { collectionsCache = await API.fetch('/collections'); } catch (e) {} }
            const others = (collectionsCache || []).filter(x => x.id !== id);
            fillGrid('cdRelatedGrid', others.length ? others : (bundlesCache || []), others.length ? collectionCardHTML : bundleCardHTML);
            renderReviews('cdReviewsList', []);
        } catch (e) { toastMsg(e.message); }
    };
    function renderCollectionDetail(c) {
        const sec = document.getElementById('collection-detail');
        const set = (sel, html) => { const el = sec.querySelector(sel); if (el) el.innerHTML = html; };
        set('.breadcrumb', t('الرئيسية / مجموعات الاستكشاف / ', 'Home / Discovery Sets / ') + esc(pname(c)));
        set('h1', esc(pname(c)));
        // السطر التعريفي (insp-text) = التاجلاين من الداشبورد (وإلا الافتراضي) — مش الوصف،
        // عشان ميتكررش مع الوصف تحت. والوصف الكامل (p-desc) = الوصف.
        const cd = parseDetailJson(c.detailJson);
        setBilNode(sec.querySelector('.insp-text'), cd.taglineAr, cd.taglineEn);
        set('.p-desc', esc(pdesc(c)));
        // الأكورديونات القابلة للتحرير (لو الأدمن دخل قيمة، نستبدلها؛ غير كده نسيب الافتراضي).
        setBilNode(document.getElementById('cdWhy'), cd.whyAr, cd.whyEn);
        setBilNode(document.getElementById('cdBox'), cd.boxAr, cd.boxEn);
        setBilNode(document.getElementById('cdBenefits'), cd.benefitsAr, cd.benefitsEn);
        // عنوان قايمة العطور المتضمنة — قابل للتحرير من الداشبورد
        setBilNode(document.getElementById('cdItemsTitle'), cd.itemsTitleAr, cd.itemsTitleEn);
        const pd = document.getElementById('cdPriceDisplay'); if (pd) pd.textContent = money(c.finalPrice);
        // السعر قبل الخصم + التوفير: يظهران فقط لو فيه توفير حقيقي في بيانات المجموعة
        const cdOrig = document.getElementById('cdOriginalPrice');
        const cdSaveWrap = document.getElementById('cdSavingsWrap');
        const cdSaveLbl = document.getElementById('cdSavingsLabel');
        const cdSavings = Number(c.savings) || 0;
        if (cdOrig) {
            const hasSave = cdSavings > 0 && Number(c.originalPrice) > Number(c.finalPrice);
            cdOrig.textContent = hasSave ? money(c.originalPrice) : '';
            cdOrig.hidden = !hasSave;
            if (cdSaveWrap) cdSaveWrap.hidden = !hasSave;
            if (cdSaveLbl && hasSave) cdSaveLbl.textContent = t('وفر ' + money(cdSavings) + ' ج.م', 'Save ' + money(cdSavings) + ' EGP');
        }
        const stockEl = sec.querySelector('.pd-stock-text');
        const stockRow = sec.querySelector('.pd-stock-row');
        if (stockRow) stockRow.hidden = false;
        if (stockEl) {
            const total = c.stock || 0;
            let status = 'in', label;
            if (total <= 0)      { status = 'out'; label = t('غير متوفر',   'Out of Stock'); }
            else if (total <= 3) { status = 'low'; label = t('كميات محدودة', 'Low Stock'); }
            else                 { status = 'in';  label = t('متاح',          'In Stock'); }
            stockEl.textContent = label;
            stockEl.setAttribute('data-ar', label); stockEl.setAttribute('data-en', label);
            if (stockRow) {
                stockRow.classList.remove('pd-stock--in', 'pd-stock--low', 'pd-stock--out');
                stockRow.classList.add('pd-stock--' + status);
            }
        }
        applyGallery(sec, '#cdSlider', '#cdSliderThumbs', [c.imageUrl, c.imageUrl2, c.imageUrl3].filter(Boolean), c.name);
        const fg = document.getElementById('cdFragrancesGrid');
        if (fg) {
            fg.innerHTML = (c.items || []).map(it => {
                const nm = biln(it.productName, it.productNameEn);
                return '<div class="cdx-fragrance" onclick="openProductDetail(\'' + it.productId + '\')">'
                + '<img src="' + esc(it.productImageUrl || '') + '" alt="' + esc(nm) + '" loading="lazy">'
                + '<div class="info"><h4>' + esc(nm) + '</h4><p>' + (c.sampleVolume || '5ML') + '</p></div></div>';
            }).join('');
        }
        collectionQty = 1;
        const qv = document.getElementById('cdQtyValue'); if (qv) qv.textContent = '1';
        const bd = document.getElementById('cdBtnPriceDisplay'); if (bd) bd.textContent = money(c.finalPrice) + ' ' + cur();
    }
    window.changeCdQty = function (delta) {
        collectionQty = Math.max(1, (collectionQty || 1) + delta);
        const v = document.getElementById('cdQtyValue'); if (v) v.textContent = collectionQty;
        const bd = document.getElementById('cdBtnPriceDisplay');
        if (bd && currentCollection) bd.textContent = money(currentCollection.finalPrice * collectionQty) + ' ' + cur();
    };
    window.addCollectionToCart = function (btn) {
        if (!currentCollection) return;
        addProductToCart({ collectionId: currentCollection.id, price: currentCollection.finalPrice, name: currentCollection.name, nameEn: currentCollection.nameEn || currentCollection.name, img: currentCollection.imageUrl, qty: collectionQty || 1 });
        if (btn) animateAddBtn(btn);
    };
    window.buyNowCollection = function () { window.addCollectionToCart(null); navigate('checkout'); };
    window.addCollectionToCartFromCard = window.storefrontAddCollectionCard;

    // Generic image-gallery applier — used by product/bundle/collection detail.
    // sliderSel must point to a `.product-slider` with `.product-slide > img` children.
    // thumbSel may be missing (we'll skip thumbnails). All non-empty `images` show; rest hidden.
    function applyGallery(sec, sliderSel, thumbSel, images, alt) {
        const gallery = (images || []).filter(Boolean);
        if (gallery.length === 0) gallery.push('');
        const slides = sec.querySelectorAll(sliderSel + ' .product-slide');
        slides.forEach((slide, i) => {
            const img = slide.querySelector('img');
            if (i < gallery.length) {
                slide.style.display = '';
                if (img) { img.src = gallery[i]; img.alt = alt || ''; img.style.filter = ''; }
            } else {
                slide.style.display = 'none';
            }
        });
        if (thumbSel) {
            const thumbs = sec.querySelectorAll(thumbSel + ' .slider-thumb');
            thumbs.forEach((thumb, i) => {
                const img = thumb.querySelector('img');
                if (i < gallery.length) {
                    thumb.style.display = '';
                    if (img) { img.src = gallery[i]; img.alt = alt || ''; img.style.filter = ''; }
                    thumb.classList.toggle('active', i === 0);
                    thumb.onclick = function () {
                        const slider = sec.querySelector(sliderSel);
                        if (!slider) return;
                        const w = slider.clientWidth;
                        slider.scrollTo({ left: i * w, behavior: 'smooth' });
                        sec.querySelectorAll(thumbSel + ' .slider-thumb').forEach(x => x.classList.remove('active'));
                        thumb.classList.add('active');
                    };
                } else {
                    thumb.style.display = 'none';
                }
            });
        }
    }
    window.applyGallery = applyGallery;

    // ---- bundle detail ----
    window.openBundleDetail = async function (id) {
        currentBundleId = id;                  // لازم قبل navigate عشان يتحفظ في sessionStorage للريفريش
        resetDetailView('bundle-detail', {
            textSelectors: ['h1', '.insp-text'],
            emptyIds: ['bdTitle', 'bdLead', 'bdPriceDisplay', 'bdOriginalPrice', 'bdSavingsLabel', 'bdOriginal', 'bdSavings', 'bdFinal', 'bdBtnPriceDisplay'],
            imageSelectors: ['#bdSlider img', '#bdSliderThumbs img'],
            clearHtmlIds: ['bdItemsList', 'bdReviewsList']
        });
        navigate('bundle-detail');
        try {
            const b = await API.fetch('/bundles/' + id);
            if (currentBundleId !== id) return;
            currentBundle = b;
            renderBundleDetail(b);
            if (!bundlesCache) { try { const d = await API.fetch('/bundles?pageSize=50'); bundlesCache = d.items || []; } catch (e) {} }
            fillGrid('bdRelatedGrid', (bundlesCache || []).filter(x => x.id !== id), bundleCardHTML);
            renderReviews('bdReviewsList', []);
        } catch (e) { toastMsg(e.message); }
    };
    window.openBundle = function (keyOrId) {
        // legacy string keys no longer used; treat as id if guid-like
        if (typeof keyOrId === 'string' && keyOrId.indexOf('-') > 0) window.openBundleDetail(keyOrId);
    };
    function renderBundleDetail(b) {
        const sec = document.getElementById('bundle-detail');
        const setId = (id, txt) => { const el = document.getElementById(id); if (el) el.textContent = txt; };
        setId('bdBreadcrumb', t('الرئيسية / الباقات / ', 'Home / Bundles / ') + pname(b));
        setId('bdTitle', pname(b));
        // السطر التعريفي: التاجلاين من الداشبورد، وإلا الوصف، وإلا عدد العطور.
        const bDetail = parseDetailJson(b.detailJson);
        const bdTagline = biln(bDetail.taglineAr, bDetail.taglineEn);
        const lead = document.getElementById('bdLead');
        if (lead) lead.textContent = bdTagline || pdesc(b) || ((b.items ? b.items.length : 0) + t(' عطور في الباقة', ' scents in this bundle'));
        // الأكورديونات القابلة للتحرير (لو الأدمن دخل قيمة نستبدلها؛ غير كده نسيب الافتراضي).
        setBilNode(document.getElementById('bdWhy'), bDetail.whyAr, bDetail.whyEn);
        setBilNode(document.getElementById('bdBenefits'), bDetail.benefitsAr, bDetail.benefitsEn);
        // كارت "إيه اللي جوه الباقة؟" — عنوانه ونصه من الداشبورد
        setBilNode(document.getElementById('bdBoxTitle'), bDetail.boxTitleAr, bDetail.boxTitleEn);
        setBilNode(document.getElementById('bdBoxText'), bDetail.boxAr, bDetail.boxEn);
        setId('bdPriceDisplay', money(b.finalPrice));
        setId('bdOriginalPrice', money(b.originalPrice));
        setId('bdSavingsLabel', t('وفر ' + money(b.savings) + ' ج.م', 'Save ' + money(b.savings) + ' EGP'));
        setId('bdOriginal', money(b.originalPrice));
        // حالة مخزون الباقة — كانت مكتوبة ثابتة ("متاحة") فما كانتش بتعكس المخزون الحقيقي
        const bdStockRow = sec.querySelector('.pd-stock-row');
        const bdStockEl = sec.querySelector('.pd-stock-text');
        if (bdStockEl) {
            const total = b.stock || 0;
            let status = 'in', label;
            if (total <= 0)      { status = 'out'; label = t('غير متوفرة',   'Out of Stock'); }
            else if (total <= 3) { status = 'low'; label = t('كميات محدودة', 'Low Stock'); }
            else                 { status = 'in';  label = t('متاحة',        'In Stock'); }
            bdStockEl.textContent = label;
            bdStockEl.setAttribute('data-ar', label); bdStockEl.setAttribute('data-en', label);
            if (bdStockRow) {
                bdStockRow.hidden = false;
                bdStockRow.classList.remove('pd-stock--in', 'pd-stock--low', 'pd-stock--out');
                bdStockRow.classList.add('pd-stock--' + status);
            }
        }
        setId('bdSavings', money(b.savings));
        setId('bdFinal', money(b.finalPrice));
        applyGallery(sec, '#bdSlider', '#bdSliderThumbs', [b.imageUrl, b.imageUrl2, b.imageUrl3].filter(Boolean), b.name);
        const list = document.getElementById('bdItemsList');
        if (list) {
            // ملاحظة: قبل كده كان حجم العطر بيتكرر مرتين (سطر .ds + شارة .vol) فبيبان "وصفين متطابقين"
            // للمنتج — شِلنا السطر المكرّر وسِبنا الاسم + شارة الحجم بس (إصلاح 4.3).
            list.innerHTML = (b.items || []).map(it => {
                const nm = biln(it.productName, it.productNameEn);
                return '<div class="bd-include-row" onclick="openProductDetail(\'' + it.productId + '\')" style="cursor:pointer;">'
                + '<img src="' + esc(it.productImageUrl || '') + '" alt="' + esc(nm) + '" loading="lazy">'
                + '<div class="info" style="flex:1;min-width:0;"><div class="nm">' + esc(nm) + '</div></div>'
                + '<span class="vol en-num">' + esc(dispVol(it.volume)) + '</span></div>';
            }).join('');
        }
        bundleQty = 1;
        const qv = document.getElementById('bdQtyValue'); if (qv) qv.textContent = '1';
        const bd = document.getElementById('bdBtnPriceDisplay'); if (bd) bd.textContent = money(b.finalPrice) + ' ' + cur();
    }
    window.changeBdQty = function (delta) {
        bundleQty = Math.max(1, (bundleQty || 1) + delta);
        const v = document.getElementById('bdQtyValue'); if (v) v.textContent = bundleQty;
        const bd = document.getElementById('bdBtnPriceDisplay');
        if (bd && currentBundle) bd.textContent = money(currentBundle.finalPrice * bundleQty) + ' ' + cur();
    };
    window.addBundleToCart = function (btn) {
        if (!currentBundle) return;
        addProductToCart({ bundleId: currentBundle.id, price: currentBundle.finalPrice, name: currentBundle.name, nameEn: currentBundle.nameEn || currentBundle.name, img: currentBundle.imageUrl, qty: bundleQty || 1 });
        if (btn) animateAddBtn(btn);
    };
    window.buyNowBundle = function () { window.addBundleToCart(null); navigate('checkout'); };
    window.addBundleByKey = window.storefrontAddBundleCard;

    // ---- checkout ----
    // السلة هي مصدر الحقيقة الوحيد: صفحة الدفع مرآة مباشرة لها — أي تعديل هنا
    // يكتب في السلة نفسها (محلية أو سيرفر) والعكس، فالاتنين متزامنين دايماً.
    window.syncCheckoutFromCart = function (keepStep) {
        // الاسم يُختار حسب اللغة الحالية — مش الاسم اللي اتخزّن وقت الإضافة للسلة،
        // وإلا منتج اتضاف والموقع عربي بيفضل عربي حتى بعد التبديل للإنجليزي.
        checkoutItems = cart.map(i => ({
            id: i.id, price: i.price, qty: i.qty,
            name: (isRtl() ? (i.name || i.nameEn) : (i.nameEn || i.name)) || i.name,
            img: i.img, volume: i.volume,
            productId: i.productId, bundleId: i.bundleId, collectionId: i.collectionId
        }));
        const list = document.getElementById('checkoutCartList');
        if (list) {
            if (!checkoutItems.length) {
                list.innerHTML = '<div style="text-align:center;padding:30px;color:var(--text-muted);">' + t('حقيبتك فارغة', 'Your bag is empty') + '</div>';
            } else {
                list.innerHTML = checkoutItems.map(item =>
                    '<div class="cart-item" id="cartItem_' + item.id + '">'
                    + '<img loading="lazy" decoding="async" src="' + esc(item.img || '') + '" class="cart-item-img" alt="' + esc(item.name) + '">'
                    + '<div class="cart-item-details"><div><h3 class="cart-item-title">' + esc(item.name) + '</h3>'
                    + '<div class="en-num" style="font-size:12px;color:#888;margin-top:4px;">' + esc(item.volume || '') + '</div></div>'
                    + '<div class="cart-qty-row">'
                    + '<div class="cart-qty-controls"><button class="cart-qty-btn" onclick="updateCartItemQty(\'' + item.id + '\',-1)">-</button>'
                    + '<span class="cart-qty-val en-num" id="cartItemQty_' + item.id + '">' + item.qty + '</span>'
                    + '<button class="cart-qty-btn" onclick="updateCartItemQty(\'' + item.id + '\',1)">+</button></div>'
                    + '<button class="cart-item-remove" onclick="removeCartItem(\'' + item.id + '\')" aria-label="Remove"><svg viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/></svg></button>'
                    + '</div>'
                    + '<div class="cart-item-price"><span class="en-num" id="cartItemPrice_' + item.id + '" data-base="' + item.price + '">'
                    + money(item.price * item.qty) + '</span> <span style="font-size:12px;">' + cur() + '</span></div></div>'
                    + '</div>'
                ).join('');
            }
        }
        calculateOrderSummary();
        // بقيت صفحة واحدة — مفيش خطوة نرجّع لها. بس دي أدق نقطة نعرف عندها إن
        // السلة اترسمت فعلاً ومعاها منتجات، فمنها بنبعت InitiateCheckout.
        if (typeof window.onCheckoutShown === 'function') window.onCheckoutShown();
    };
    // ===== حفظ بيانات الشحن أثناء الكتابة — الريفريش لا يمسح ما كتبه العميل =====
    const _SHIP_FIELDS = ['shipFirstName', 'shipLastName', 'shipPhone', 'shipEmail', 'shipGovernorate', 'shipCity', 'shipAddress'];
    function _persistShipField(e) {
        if (!e.target || _SHIP_FIELDS.indexOf(e.target.id) === -1) return;
        try { sessionStorage.setItem('remal_ship_' + e.target.id, e.target.value || ''); } catch (err) {}
        // تغيير المحافظة يغيّر تكلفة الشحن → نعيد حساب الملخص فورًا
        if (e.target.id === 'shipGovernorate' && typeof calculateOrderSummary === 'function') {
            try { calculateOrderSummary(); } catch (err) {}
        }
    }
    document.addEventListener('input', _persistShipField);
    document.addEventListener('change', _persistShipField);
    document.addEventListener('remal:navigated', function (e) {
        if (e.detail !== 'checkout') return;
        const restore = function (id) {
            const el = document.getElementById(id);
            if (!el || el.value) return;
            try {
                const v = sessionStorage.getItem('remal_ship_' + id);
                if (v) el.value = v;
            } catch (err) {}
        };
        // المحافظة الأول، وبعدين نبني مدنها، وبعدين نستعيد المدينة — وإلا الـ select
        // بتاع المدينة يبقى فاضي من الخيارات فقيمتها المحفوظة تضيع.
        restore('shipGovernorate');
        if (typeof onGovernorateChange === 'function') { try { onGovernorateChange(true); } catch (err) {} }
        _SHIP_FIELDS.filter(function (id) { return id !== 'shipGovernorate'; }).forEach(restore);
        // بعد استعادة المحافظة المحفوظة، الشحن لازم يعكسها
        if (typeof calculateOrderSummary === 'function') { try { calculateOrderSummary(); } catch (err) {} }
    });

    window.updateCartItemQty = async function (id, delta) {
        const item = cart.find(i => i.id === id);
        if (!item) return;
        const newQty = Math.max(1, item.qty + delta); // الحذف من صفحة الدفع بزر السلة فقط
        if (newQty === item.qty) return;
        if (API.isAuthed() && !String(id).startsWith('L')) {
            try {
                await API.fetch('/cart/items/' + id, { method: 'PUT', body: { quantity: newQty } });
                await refreshServerCart();
            } catch (e) { toastMsg(e.message); return; }
        } else {
            item.qty = newQty;
            saveGuestCart();
        }
        updateCartBadge();
        renderCartDrawer();
        syncCheckoutFromCart(true);
    };
    window.removeCartItem = async function (id) {
        const el = document.getElementById('cartItem_' + id);
        if (el) { el.style.opacity = '0'; el.style.transition = '0.3s'; }
        if (API.isAuthed() && !String(id).startsWith('L')) {
            try {
                await API.fetch('/cart/items/' + id, { method: 'DELETE' });
                await refreshServerCart();
            } catch (e) { toastMsg(e.message); if (el) { el.style.opacity = ''; } return; }
        } else {
            cart = cart.filter(i => i.id !== id);
            saveGuestCart();
        }
        setTimeout(() => {
            updateCartBadge();
            renderCartDrawer();
            syncCheckoutFromCart(true);
        }, 250);
    };
    window.calculateOrderSummary = function () {
        const subtotal = (checkoutItems || []).reduce((s, i) => s + i.price * i.qty, 0);
        // تكلفة الشحن حسب المحافظة المختارة (نفس منطق السيرفر بالظبط) — والمجاني يتفوق عليها
        const govFee = (typeof shippingFeeForGovernorate === 'function')
            ? shippingFeeForGovernorate(typeof selectedGovernorate === 'function' ? selectedGovernorate() : '')
            : (shippingFee || 60);
        const ship = (subtotal >= freeShippingThreshold || subtotal === 0) ? 0 : govFee;
        const paymentDiscount = subtotal * (appliedPaymentDiscount || 0);
        const discountAmt = appliedCouponDiscount + paymentDiscount;
        const total = Math.max(0, subtotal + ship - discountAmt);
        const set = (id, txt) => { const el = document.getElementById(id); if (el) el.textContent = txt; };
        set('sumSubtotal', money(subtotal));
        const sh = document.getElementById('sumShipping');
        if (sh) {
            const _hasRates = (typeof hasZonePricing === 'function') && hasZonePricing();
            const _gov = (typeof selectedGovernorate === 'function') ? selectedGovernorate() : '';
            if (ship === 0) sh.textContent = t('مجاني', 'FREE');
            else if (_hasRates && !_gov)
                // فيه أسعار مختلفة حسب المحافظة ولسه ماختارهاش → نوضح إن الرقم تقديري
                sh.textContent = t(ship + ' ج.م (يُحدَّد بالمحافظة)', ship + ' EGP (by governorate)');
            else sh.textContent = ship + ' EGP';
        }
        const dr = document.getElementById('sumDiscountRow'); if (dr) dr.style.display = discountAmt > 0 ? 'flex' : 'none';
        set('sumDiscountVal', money(Math.round(discountAmt)));
        set('sumFinalTotal', money(Math.round(total)));
    };
    window.applyPromo = async function () {
        const input = document.getElementById('promoInput');
        const msg = document.getElementById('promoMsg');
        const val = (input.value || '').trim().toUpperCase();
        const subtotal = (checkoutItems || []).reduce((s, i) => s + i.price * i.qty, 0);
        if (!val) { appliedCouponDiscount = 0; appliedCouponCode = null; msg.textContent = ''; msg.className = 'promo-msg'; calculateOrderSummary(); return; }
        try {
            const r = await API.fetch('/coupons/validate', { method: 'POST', noAuth: true, body: { code: val, orderAmount: subtotal } });
            if (r.valid) {
                appliedCouponDiscount = r.discountAmount; appliedCouponCode = val;
                const okText = '✓ ' + t('تم تطبيق الخصم: ', 'Discount applied: ') + money(r.discountAmount) + ' ' + cur();
                msg.textContent = okText;
                msg.className = 'promo-msg success';
                toastMsg(okText);
            } else {
                appliedCouponDiscount = 0; appliedCouponCode = null;
                msg.textContent = r.reason || t('كود غير صحيح', 'Invalid code');
                msg.className = 'promo-msg error';
            }
        } catch (e) {
            appliedCouponDiscount = 0; appliedCouponCode = null;
            msg.textContent = e.message; msg.className = 'promo-msg error';
        }
        calculateOrderSummary();
    };
    function saveMyOrderCode(code) {
        try {
            const list = JSON.parse(localStorage.getItem('remal_my_orders') || '[]');
            if (!list.includes(code)) { list.unshift(code); localStorage.setItem('remal_my_orders', JSON.stringify(list.slice(0, 30))); }
        } catch (e) {}
    }
    window.placeOrder = async function () {
        // اتشالت بوابة "أكمل كل الخطوات أولاً" — مفيش خطوات أصلاً دلوقتي.
        // التحقق كله بيحصل هنا مرة واحدة عند التأكيد، وكل خطأ بيوجّه العميل
        // لمكانه بالظبط بدل رسالة عامة.
        const firstName = ((document.getElementById('shipFirstName') || {}).value || '').trim();
        const lastName  = ((document.getElementById('shipLastName')  || {}).value || '').trim();
        const phone     = ((document.getElementById('shipPhone')     || {}).value || '').trim();
        const email     = ((document.getElementById('shipEmail')      || {}).value || '').trim();
        const gov       = ((document.getElementById('shipGovernorate') || {}).value || '').trim();
        const cityVal   = (typeof selectedCity === 'function') ? selectedCity() : '';
        const address   = ((document.getElementById('shipAddress')   || {}).value || '').trim();
        // المدينة مطلوبة فقط لو المحافظة متضافلها مدن من لوحة التحكم
        const cityWrap  = document.getElementById('shipCityWrap');
        const cityNeeded = !!(cityWrap && !cityWrap.hidden);
        // التحقق التفصيلي (بيلوّن الحقول الغلط ويركّز على أولها) — كان بيتنفّذ
        // عند الانتقال من خطوة ٢ لـ ٣، ولازم ينتقل هنا بعد ما الخطوات اتشالت.
        if (typeof validateShippingForm === 'function' && !validateShippingForm({ focusFirstError: true })) {
            try { toastMsg(t('برجاء تصحيح البيانات الموضحة بالأحمر', 'Please fix the highlighted fields')); } catch (e) {}
            return;
        }
        if (!firstName || !phone || !address || !gov || (cityNeeded && !cityVal)) {
            alert(t('برجاء ملء بيانات الشحن كاملة', 'Please fill in all shipping details'));
            navigate('checkout'); goToStep(2); return;
        }
        // حفظ البيانات للطلبات الجاية — كان مربوطًا بالانتقال بين الخطوات.
        try {
            const cb = document.getElementById('saveInfoCheck');
            if (cb && cb.checked) {
                sessionStorage.setItem('remal_saved_shipping', JSON.stringify({
                    firstName: firstName, lastName: lastName, phone: phone,
                    governorate: gov, city: cityVal, email: email, address: address
                }));
            } else {
                sessionStorage.removeItem('remal_saved_shipping');
            }
        } catch (e) {}
        if (!checkoutItems.length) { alert(t('حقيبتك فارغة', 'Your bag is empty')); return; }
        // تحقق مرجع التحويل — رسالة تحت الحقل نفسه بدل alert، ووقف الإرسال لحد ما يبقى صحيح
        if (!window.validatePaymentRef || !window.validatePaymentRef(true)) {
            navigate('checkout'); goToStep(3);
            const el = document.getElementById(selectedPayment === 'insta' ? 'instaInput' : 'walletInput');
            if (el) { try { el.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch (e2) {} el.focus(); }
            return;
        }
        const payMap = { cod: 'CashOnDelivery', insta: 'InstaPay', wallet: 'Wallet' };
        const items = checkoutItems.map(it => it.bundleId ? { bundleId: it.bundleId, quantity: it.qty }
            : it.collectionId ? { collectionId: it.collectionId, quantity: it.qty }
            : { productId: it.productId, volume: it.volume, quantity: it.qty });
        // Combine city + governorate for city field; full street address goes in customerAddress
        const cityFull = cityVal ? (cityVal + ' — ' + gov) : gov;
        // Advanced Matching: هنا عندنا الاسم والموبايل والمدينة مؤكدين — بنعرّف
        // البيكسل بيهم قبل حدث الشراء عشان يتبعت بأعلى جودة مطابقة ممكنة.
        try {
            window.RemalTrack.identify({
                email: email, phone: phone, firstName: firstName, lastName: lastName,
                city: gov, externalId: email || phone
            });
        } catch (e) {}
        // تتبع: نولّد معرّف الشراء **قبل** الإرسال ونبعته مع الطلب، فالسيرفر
        // يستخدم نفس المعرّف في Conversions API و Meta تدمج النسختين بدل التكرار.
        let _sig = { eventId: null, fbp: '', fbc: '', sourceUrl: location.href };
        try { _sig = window.RemalTrack.signals(); } catch (e) {}
        const dto = {
            customerName: (firstName + ' ' + lastName).trim(),
            customerPhone: phone, customerAddress: address, city: cityFull,
            customerEmail: email || null,
            paymentMethod: payMap[selectedPayment] || 'CashOnDelivery',
            couponCode: appliedCouponCode || null,
            giftWrap: false, notes: '', items: items,
            eventId: _sig.eventId, fbp: _sig.fbp, fbc: _sig.fbc, sourceUrl: _sig.sourceUrl
        };
        const btn = document.getElementById('btnPlaceOrder');
        const orig = btn ? btn.innerHTML : '';
        if (btn) { btn.style.opacity = '0.6'; btn.style.pointerEvents = 'none'; btn.innerHTML = '<span>...</span>'; }
        try {
            const order = await API.fetch('/orders', { method: 'POST', body: dto });
            // تتبع: الشراء — الحدث اللي الخوارزمية بتتعلم منه مين بيشتري فعلاً
            try {
                window.RemalTrack.event('purchase', {
                    eventId: _sig.eventId,
                    transactionId: order.code,
                    value: order.total,
                    shipping: order.shippingFee,
                    coupon: appliedCouponCode || '',
                    items: (checkoutItems || []).map(function (i) {
                        return { id: i.productId || i.bundleId || i.collectionId, name: i.nameEn || i.name,
                                 variant: i.volume, price: i.price, quantity: i.qty };
                    })
                });
                window.__trackedBeginCheckout = false;
                window.__trackedPaymentInfo = false;
            } catch (e) {}
            const idEl = document.getElementById('displayOrderId');
            if (idEl) idEl.textContent = order.code;
            saveMyOrderCode(order.code);
            if (API.isAuthed()) { try { await API.fetch('/cart', { method: 'DELETE' }); } catch (e) {} }
            cart = [];
            saveGuestCart();
            updateCartBadge(); renderCartDrawer();
            appliedCouponDiscount = 0; appliedCouponCode = null;
            toastMsg(t('تم استلام طلبك ✓ — رقم الطلب: ' + order.code, 'Order received ✓ — code: ' + order.code));
            navigate('order-success');
        } catch (e) {
            toastMsg(e.message);
        } finally {
            if (btn) { btn.style.opacity = '1'; btn.style.pointerEvents = 'auto'; btn.innerHTML = orig; }
        }
    };

    // ---- order tracking ----
    const STATUS_AR = { Pending: 'قيد المراجعة', Preparing: 'قيد التجهيز', Shipping: 'في الطريق', Delivered: 'تم التسليم', Cancelled: 'ملغي', Refunded: 'مرتجع' };
    const STATUS_EN = { Pending: 'Pending', Preparing: 'Preparing', Shipping: 'Out for Delivery', Delivered: 'Delivered', Cancelled: 'Cancelled', Refunded: 'Refunded' };
    // Silent poll the tracking endpoint every 20s while a tracking result is visible,
    // so the customer sees status changes (Preparing → Shipping → Delivered) without
    // having to manually refresh or click track again.
    let _trackPollTimer = null;
    let _trackPollLastStatus = null;
    function startTrackPolling(code) {
        stopTrackPolling();
        if (!code) return;
        _trackPollTimer = setInterval(async () => {
            const box = document.getElementById('trackingResultBox');
            if (!box || !box.classList.contains('show')) { stopTrackPolling(); return; }
            const onPage = document.querySelector('#tracking.page-section.active, #tracking.active');
            if (!onPage) { stopTrackPolling(); return; }
            try {
                let detail = null;
                try { detail = await API.fetch('/orders/by-code/' + encodeURIComponent(code), { noAuth: true }); }
                catch (e) {}
                if (!detail) detail = await API.fetch('/orders/track/' + encodeURIComponent(code), { noAuth: true });
                if (detail && detail.status && detail.status !== _trackPollLastStatus) {
                    renderTracking(detail);
                    if (_trackPollLastStatus) {
                        toastMsg(t('تحديث: ' + (STATUS_AR[detail.status] || detail.status), 'Update: ' + (STATUS_EN[detail.status] || detail.status)));
                    }
                    _trackPollLastStatus = detail.status;
                }
            } catch (e) { /* offline / fail silently */ }
        }, 20000);
    }
    function stopTrackPolling() {
        if (_trackPollTimer) { clearInterval(_trackPollTimer); _trackPollTimer = null; }
    }
    // Stop polling when the user navigates away from the tracking page
    document.addEventListener('remal:navigated', (e) => {
        if (e && e.detail !== 'tracking') stopTrackPolling();
    });
    window.trackOrderNow = async function () {
        const input = document.getElementById('trackInput');
        const btn = document.getElementById('btnTrackSubmit');
        const box = document.getElementById('trackingResultBox');
        const code = (input.value || '').trim();
        if (!code) { toastMsg(t('برجاء إدخال رقم الطلب', 'Please enter your order number')); return; }
        btn.classList.add('loading');
        box.classList.remove('show');
        try {
            // Bug 5: /orders/track returns only timeline fields. We want items, total, payment,
            // address — all of which live on /orders/by-code (also public). Use it as the
            // primary source; fall back to /orders/track if by-code happens to return nothing.
            let detail = null;
            try { detail = await API.fetch('/orders/by-code/' + encodeURIComponent(code), { noAuth: true }); }
            catch (e) { /* fall through to /track */ }
            if (!detail) detail = await API.fetch('/orders/track/' + encodeURIComponent(code), { noAuth: true });
            renderTracking(detail);
            box.classList.add('show');
            box.scrollIntoView({ behavior: 'smooth', block: 'center' });
            _trackPollLastStatus = detail.status || null;
            startTrackPolling(code);
            // حفظ رقم الطلب في الـ URL و localStorage — يبقى موجوداً بعد الريفريش
            try {
                localStorage.setItem('remal_last_track', code);
                const u = new URL(location.href);
                u.searchParams.set('code', code);
                history.replaceState(history.state, '', u.toString());
            } catch (e) {}
        } catch (e) {
            toastMsg(e.message);
        } finally {
            btn.classList.remove('loading');
        }
    };
    // استرجاع رقم التتبع تلقائياً عند فتح الصفحة (من الـ URL أو آخر تتبع محفوظ)
    document.addEventListener('remal:navigated', function (e) {
        if (e.detail !== 'tracking') return;
        const input = document.getElementById('trackInput');
        if (!input || input.value.trim()) return;
        let code = '';
        try {
            code = new URL(location.href).searchParams.get('code') || localStorage.getItem('remal_last_track') || '';
        } catch (err) {}
        if (code) { input.value = code; window.trackOrderNow(); }
    });

    // استرجاع آخر تبويب في صفحة "حسابي" بعد الريفريش
    document.addEventListener('remal:navigated', function (e) {
        if (e.detail !== 'account') return;
        let tab = '';
        try { tab = sessionStorage.getItem('remal_account_tab') || ''; } catch (err) {}
        if (tab && tab !== 'profile' && typeof switchAccountTab === 'function') {
            setTimeout(function () { switchAccountTab(tab); }, 80);
        }
    });

    function renderTracking(tr) {
        const idEl = document.getElementById('resOrderId');
        if (idEl) idEl.textContent = tr.code;
        const badge = document.getElementById('resStatusBadge') || document.querySelector('#trackingResultBox .tr-status-badge');
        const badgeText = badge ? badge.querySelector('span') : null;
        if (badgeText) badgeText.textContent = t(STATUS_AR[tr.status] || tr.status, STATUS_EN[tr.status] || tr.status);
        if (badge) {
            badge.classList.remove('tr-status-delivered', 'tr-status-cancelled');
            if (tr.status === 'Delivered') badge.classList.add('tr-status-delivered');
            else if (tr.status === 'Cancelled' || tr.status === 'Refunded') badge.classList.add('tr-status-cancelled');
        }
        const order = ['Pending', 'Preparing', 'Shipping', 'Delivered'];
        let activeIdx = order.indexOf(tr.status);
        if (tr.status === 'Cancelled' || tr.status === 'Refunded') activeIdx = 0;
        // Progress percentage: Pending=10, Preparing=40, Shipping=70, Delivered=100
        const pctMap = { Pending: 10, Preparing: 40, Shipping: 70, Delivered: 100, Cancelled: 0, Refunded: 0 };
        const pct = pctMap[tr.status] != null ? pctMap[tr.status] : 0;
        const fill = document.getElementById('resProgressFill');
        const pctEl = document.getElementById('resProgressPct');
        if (fill) fill.style.width = pct + '%';
        if (pctEl) pctEl.textContent = pct + '%';
        const items = document.querySelectorAll('#trackingResultBox .timeline-item');
        const dates = [tr.placedAt, tr.preparedAt, tr.shippedAt, tr.deliveredAt];
        items.forEach((el, i) => {
            el.classList.remove('completed', 'active');
            if (i < activeIdx) el.classList.add('completed');
            else if (i === activeIdx) el.classList.add('active');
            const dateEl = el.querySelector('.tl-date');
            if (dateEl) {
                if (dates[i]) {
                    const d = new Date(dates[i]);
                    const datStr = d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
                    const timStr = d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
                    dateEl.classList.remove('tl-date-pending');
                    dateEl.innerHTML = '<svg viewBox="0 0 24 24" style="width:11px;height:11px;stroke:currentColor;fill:none;stroke-width:2;"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg><span>' + datStr + ' · ' + timStr + '</span>';
                } else {
                    dateEl.classList.add('tl-date-pending');
                    dateEl.textContent = t('في الانتظار', 'Pending');
                }
            }
        });
        // Update the static address + payment + add an items panel (Bug 5)
        const footer = document.querySelector('#trackingResultBox .tracking-details-footer');
        if (footer && tr.items) {
            const PAY = { CashOnDelivery: [t('الدفع عند الاستلام', 'Cash on delivery')], InstaPay: [t('إنستا باي', 'InstaPay')], Wallet: [t('محفظة إلكترونية', 'E-wallet')] };
            const payLabel = (PAY[tr.paymentMethod] && PAY[tr.paymentMethod][0]) || tr.paymentMethod || '';
            const cur = t('ج.م', 'EGP');
            footer.innerHTML = ''
                + '<div class="td-block"><h4>' + t('عنوان التوصيل', 'Delivery address') + '</h4>'
                +   '<p>' + esc((tr.customerAddress || '') + (tr.city ? ' — ' + tr.city : '')) + '</p>'
                +   '<p style="font-size:12px;color:var(--text-muted);">' + esc(tr.customerName || '') + ' · <span class="en-num">' + esc(tr.customerPhone || '') + '</span></p>'
                + '</div>'
                + '<div class="td-block"><h4>' + t('طريقة الدفع', 'Payment') + '</h4>'
                +   '<p>' + esc(payLabel) + (tr.paymentStatus === 'Paid' ? ' · ' + t('تم الدفع', 'Paid') : '') + '</p>'
                +   '<p style="font-size:12px;color:var(--text-muted);"><span class="en-num">' + Number(tr.total || 0).toLocaleString('en-US') + '</span> ' + cur + '</p>'
                + '</div>';
            let itemsPanel = document.getElementById('trackingItemsPanel');
            if (!itemsPanel) {
                itemsPanel = document.createElement('div');
                itemsPanel.id = 'trackingItemsPanel';
                itemsPanel.style.cssText = 'padding:20px;border-top:1px solid var(--border-color);';
                footer.parentNode.appendChild(itemsPanel);
            }
            const itemsHtml = (tr.items || []).map(it => {
                const name = it.itemName || '—';
                const vol = it.volume ? ' · <span class="en-num">' + esc(dispVol(it.volume)) + '</span>' : '';
                const img = it.imageUrl ? '<img loading="lazy" decoding="async" src="' + esc(it.imageUrl) + '" alt="' + esc(name) + '" style="width:48px;height:48px;border-radius:6px;object-fit:cover;flex-shrink:0;border:1px solid var(--border-color);">' : '';
                return ''
                    + '<div style="display:flex;gap:12px;align-items:center;padding:10px 0;border-bottom:1px solid var(--border-color);">'
                    +   img
                    +   '<div style="flex:1;min-width:0;">'
                    +     '<div style="font-weight:700;font-size:14px;">' + esc(name) + vol + '</div>'
                    +     '<div style="font-size:12px;color:var(--text-muted);"><span class="en-num">' + Number(it.unitPrice || 0).toLocaleString('en-US') + '</span> ' + cur + ' × <span class="en-num">' + (it.quantity || 1) + '</span></div>'
                    +   '</div>'
                    +   '<div style="font-weight:700;font-family:Montserrat,sans-serif;"><span class="en-num">' + Number(it.lineTotal || 0).toLocaleString('en-US') + '</span> ' + cur + '</div>'
                    + '</div>';
            }).join('');
            itemsPanel.innerHTML = ''
                + '<h4 style="font-size:14px;font-weight:700;margin-bottom:14px;">' + t('تفاصيل الطلب', 'Order items') + ' (<span class="en-num">' + (tr.items || []).length + '</span>)</h4>'
                + itemsHtml
                + '<div style="margin-top:14px;display:flex;justify-content:space-between;align-items:center;border-top:2px solid var(--black);padding-top:12px;">'
                +   '<span style="font-weight:700;font-size:14px;">' + t('الإجمالي', 'Total') + '</span>'
                +   '<span style="font-weight:800;font-family:Montserrat,sans-serif;font-size:16px;"><span class="en-num">' + Number(tr.total || 0).toLocaleString('en-US') + '</span> ' + cur + '</span>'
                + '</div>';
        } else {
            // No items in payload (e.g. /track fallback) — remove any previously-injected items panel
            const stale = document.getElementById('trackingItemsPanel');
            if (stale) stale.remove();
        }
    }

    // ---- newsletter + contact ----
    window.submitNewsletter = async function (form) {
        const email = form.querySelector('input[type=email]').value.trim();
        const msg = form.querySelector('.newsletter-msg');
        try {
            await API.fetch('/newsletter/subscribe', { method: 'POST', noAuth: true, body: { email: email, source: 'footer' } });
            msg.style.color = 'var(--green)';
            msg.textContent = t('تم الاشتراك في النشرة 🤍', 'Subscribed 🤍');
            form.reset();
        } catch (e) {
            msg.style.color = 'var(--red)';
            msg.textContent = e.message;
        }
    };
    window.submitContact = async function (form) {
        const fd = new FormData(form);
        const msg = form.querySelector('.contact-msg');
        try {
            await API.fetch('/contact', { method: 'POST', noAuth: true, body: {
                name: fd.get('name'), phone: fd.get('phone'), email: fd.get('email') || null, message: fd.get('message')
            } });
            msg.style.color = 'var(--green)';
            msg.textContent = t('وصلتنا رسالتك — هنرد عليك في أسرع وقت 🤍', 'Message received — we will reply soon 🤍');
            form.reset();
        } catch (e) {
            msg.style.color = 'var(--red)';
            msg.textContent = e.message;
        }
    };

    // ---- auth ----
    // ================= GOOGLE SIGN-IN =================
    // Set window.GOOGLE_CLIENT_ID before this script runs to enable.
    // Otherwise GSI button is hidden and only email/password login is shown.
    function _getGoogleClientId() {
        // Inline override > meta tag > window global
        const meta = document.querySelector('meta[name="google-client-id"]');
        return window.GOOGLE_CLIENT_ID || (meta && meta.content) || '';
    }
    function _afterGoogleLogin(data) {
        try {
            API.setToken(data.accessToken);
            isLoggedIn = true;
            try { sessionStorage.setItem('remal_loggedIn', 'true'); } catch (e) {}
            // Fetch saved shipping profile so future checkouts autofill
            loadSavedShippingFromServer().catch(() => {});
            mergeGuestCart().catch(() => {}).then(() => { updateCartBadge(); renderCartDrawer(); });
            syncWishlistFromServer().catch(() => {}).then(() => updateWishlistBadge());
            if (typeof updateAuthUI === 'function') updateAuthUI();
            const firstName = (data.user && data.user.fullName) ? data.user.fullName.split(' ')[0] : '';
            toastMsg(firstName ? t('أهلاً بيك ' + firstName + ' 🤍', 'Welcome ' + firstName + ' 🤍') : t('أهلاً بيك 🤍', 'Welcome 🤍'));
            navigate('home');
        } catch (e) { toastMsg(e.message); }
    }
    window.handleGoogleCredential = async function (response) {
        if (!response || !response.credential) { toastMsg(t('فشل تسجيل الدخول بجوجل', 'Google sign-in failed')); return; }
        try {
            const data = await API.fetch('/auth/google', { method: 'POST', noAuth: true, body: { credential: response.credential } });
            _afterGoogleLogin(data);
        } catch (e) { toastMsg(e.message); }
    };
    // زر جوجل بيترندر جوه iframe، ولغته بتتحدد من مكتبة GSI وقت تحميلها — مش من
    // خيارات renderButton. فعشان اللغة تتغير بدون ريفريش لازم نعيد تحميل المكتبة
    // نفسها بـ ?hl=<lang> ونمسح حالتها القديمة، وبعدها نعيد رسم الأزرار.
    let _gsiReloading = false;
    // تبديل اللغة بيحصل في أي صفحة، لكن زر جوجل موجود في الدخول/التسجيل بس.
    // فبنأجّل إعادة تحميل المكتبة لحد ما المستخدم يوصل الصفحة دي فعلاً — كده
    // التصفّح العادي ما بيولّدش أي إعادة تحميل (ولا تحذير GSI في الـ console).
    window.reloadGsiForLang = function (lang) {
        if (_gsiReloading) return;
        if (window.__gsiLoadedLang === lang) { window.__gsiPendingLang = null; return; }
        const onAuthPage = !!document.querySelector('#login.active, #register.active');
        if (!onAuthPage) { window.__gsiPendingLang = lang; return; }   // نأجّلها
        window.__gsiPendingLang = null;
        const wraps = ['gsiLoginBtn', 'gsiRegisterBtn'].map(id => document.getElementById(id)).filter(Boolean);
        if (!wraps.length) return;
        _gsiReloading = true;
        wraps.forEach(w => { w.innerHTML = ''; });
        document.querySelectorAll('script[src*="gsi/client"]').forEach(s => s.remove());
        try { delete window.google.accounts; } catch (e) { if (window.google) window.google.accounts = undefined; }
        window.__gsiInitialized = false;   // المكتبة الجديدة محتاجة initialize من الأول
        const s = document.createElement('script');
        s.src = 'https://accounts.google.com/gsi/client?hl=' + encodeURIComponent(lang);
        s.async = true; s.defer = true;
        s.onload = function () {
            window.__gsiLoadedLang = lang;
            _gsiReloading = false;
            renderGsiButtons(true);
        };
        s.onerror = function () { _gsiReloading = false; };
        document.head.appendChild(s);
    };
    // force = إعادة رسم إجبارية (عند تبديل اللغة) حتى لو الزر مرسوم بالفعل
    function renderGsiButtons(force) {
        if (!window.google || !google.accounts || !google.accounts.id) return false;
        if (force) { ['gsiLoginBtn', 'gsiRegisterBtn'].forEach(id => { const e = document.getElementById(id); if (e) e.innerHTML = ''; }); }
        const clientId = _getGoogleClientId();
        if (!clientId) {
            // No client ID configured — hide the GSI containers gracefully
            ['gsiLoginBtn', 'gsiRegisterBtn'].forEach(id => { const el = document.getElementById(id); if (el) el.style.display = 'none'; });
            const divs = document.querySelectorAll('#login .auth-divider, #register .auth-divider');
            divs.forEach(d => d.style.display = 'none');
            return true;
        }
        try {
            // initialize() مرة واحدة فقط لكل تحميل للمكتبة — استدعاؤها مع كل زيارة
            // لصفحة الدخول كان بيطلع تحذير GSI_LOGGER في الـ console ("called multiple times").
            // بنصفّر العلامة في reloadGsiForLang لأن المكتبة ساعتها بتتحمّل من جديد.
            if (!window.__gsiInitialized) {
                google.accounts.id.initialize({
                    client_id: clientId,
                    callback: window.handleGoogleCredential,
                    ux_mode: 'popup',
                    auto_select: false,
                });
                window.__gsiInitialized = true;
            }
            ['gsiLoginBtn', 'gsiRegisterBtn'].forEach(id => {
                const el = document.getElementById(id);
                if (!el) return;
                el.innerHTML = '';
                // العرض لازم يتقاس ديناميكياً: العرض الثابت ٣٦٠ كان بيطلّع iframe أعرض
                // من شاشات أندرويد الضيقة (٣٦٠px وأقل) → سكرول أفقي يقصّ الصفحة كلها.
                // لو الحاوية مخفية وقت الرندر (clientWidth = 0) نقيس من عرض الشاشة ناقص الحشو.
                const gsiW = Math.max(180, Math.min(360, el.clientWidth || (window.innerWidth - 48)));
                google.accounts.id.renderButton(el, {
                    type: 'standard', theme: 'outline', size: 'large',
                    text: 'continue_with', shape: 'rectangular', logo_alignment: 'center',
                    width: gsiW,
                    // زر جوجل بيترندر داخل iframe بلغة ثابتة وقت الرسم — لازم نمرر اللغة
                    // ونعيد رسمه عند التبديل، وإلا يفضل عربي في الواجهة الإنجليزية.
                    locale: (document.documentElement.getAttribute('lang') === 'en' ? 'en' : 'ar'),
                });
            });
            return true;
        } catch (e) { console.warn('GSI init failed', e); return false; }
    }
    // Poll briefly until the GSI library is on window, then render once.
    (function waitForGsi() {
        let tries = 0;
        const tick = () => {
            // __gsiLoadedLang اتظبطت وقت حقن سكربت المكتبة في الـ head (بـ ?hl=)
            if (renderGsiButtons()) return;
            if (++tries < 40) setTimeout(tick, 250);
        };
        window.renderGsiButtons = renderGsiButtons;   // عشان applyLanguage تقدر تعيد رسمه
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', tick);
        else tick();
    })();
    // لو المستخدم لفّ الموبايل وعرض الشاشة اتغير فعلاً (مش الكيبورد) نعيد قياس زر جوجل
    let _gsiLastW = window.innerWidth;
    window.addEventListener('resize', () => {
        if (Math.abs(window.innerWidth - _gsiLastW) < 40) return;
        _gsiLastW = window.innerWidth;
        if (document.querySelector('#login.active, #register.active')) setTimeout(renderGsiButtons, 200);
    });
    // Re-render on every visit to login/register (in case DOM re-mounted)
    document.addEventListener('remal:navigated', (e) => {
        if (e && (e.detail === 'login' || e.detail === 'register')) {
            // لو اللغة اتغيّرت وإحنا في صفحة تانية، دلوقتي وقت تنفيذ إعادة التحميل المؤجَّلة
            const pending = window.__gsiPendingLang;
            if (pending && pending !== window.__gsiLoadedLang && typeof window.reloadGsiForLang === 'function') {
                setTimeout(() => window.reloadGsiForLang(pending), 30);
                return;
            }
            setTimeout(renderGsiButtons, 50);
        }
    });

    // ================= SAVED SHIPPING ADDRESS (server-backed for logged-in users) =================
    window.loadSavedShippingFromServer = async function () {
        if (!API.isAuthed()) return null;
        try {
            const p = await API.fetch('/auth/shipping-profile');
            if (p && (p.addressLine || p.city || p.governorate)) {
                const data = {
                    firstName: p.firstName || '',
                    lastName: p.lastName || '',
                    phone: p.phone || '',
                    governorate: p.governorate || '',
                    city: p.city || '',
                    address: p.addressLine || ''
                };
                sessionStorage.setItem('remal_saved_shipping', JSON.stringify(data));
            }
            return p;
        } catch (e) { return null; }
    };
    window.saveShippingProfileToServer = async function (data) {
        if (!API.isAuthed()) return;
        try {
            await API.fetch('/auth/shipping-profile', { method: 'PUT', body: {
                firstName: data.firstName, lastName: data.lastName,
                phone: data.phone, governorate: data.governorate,
                city: data.city, addressLine: data.address
            } });
        } catch (e) { /* silent */ }
    };

    // ===== إظهار/إخفاء الباسورد (العين) =====
    window.togglePw = function (btn) {
        const input = btn.closest('.pw-wrap')?.querySelector('input');
        if (!input) return;
        const show = input.type === 'password';
        input.type = show ? 'text' : 'password';
        btn.classList.toggle('showing', show);
    };

    // ===== نسيت الباسورد: إرسال رابط الاستعادة =====
    window.handleForgotPassword = async function () {
        const email = ((document.getElementById('fpEmail') || {}).value || '').trim();
        if (!email) return;
        const btn = document.getElementById('fpSubmitBtn');
        if (btn) { btn.style.opacity = '0.6'; btn.style.pointerEvents = 'none'; }
        try {
            await API.fetch('/auth/forgot-password', { method: 'POST', noAuth: true, body: { email: email } });
        } catch (e) { /* رد صامت دائماً — لا نكشف وجود الحساب من عدمه */ }
        if (btn) { btn.style.opacity = ''; btn.style.pointerEvents = ''; }
        const msg = document.getElementById('fpMsg');
        if (msg) {
            msg.style.display = 'block';
            msg.textContent = t('لو الإيميل ده مسجل عندنا، هيوصلك رابط تعيين كلمة السر خلال دقايق. افحص صندوق الوارد و"الرسائل غير المرغوبة".',
                                'If this email is registered, a reset link is on its way. Check your inbox and spam folder.');
        }
    };

    // عند فتح صفحة إعادة التعيين: تحقق من صلاحية الرابط قبل عرض النموذج.
    // لو مستخدم/منتهي → أعرض رسالة "الرابط غير صالح" بدل النموذج.
    document.addEventListener('remal:navigated', async function (e) {
        if (e.detail !== 'reset-password') return;
        const formBox = document.getElementById('resetFormBox');
        const invalidBox = document.getElementById('resetInvalidBox');
        if (!formBox || !invalidBox) return;
        let email = '', token = '';
        try { const q = new URLSearchParams(location.search); email = q.get('email') || ''; token = q.get('t') || ''; } catch (err) {}
        // ابدأ بإخفاء الاتنين لحين التحقق
        formBox.style.display = 'none'; invalidBox.style.display = 'none';
        if (!email || !token) { invalidBox.style.display = ''; localize(invalidBox); return; }
        try {
            const r = await API.fetch('/auth/verify-reset-token?email=' + encodeURIComponent(email) + '&token=' + encodeURIComponent(token), { noAuth: true });
            if (r && r.valid) { formBox.style.display = ''; }
            else { invalidBox.style.display = ''; localize(invalidBox); }
        } catch (err) { invalidBox.style.display = ''; localize(invalidBox); }
    });

    // ===== تعيين كلمة سر جديدة (من رابط الإيميل: /reset-password?email=..&t=..) =====
    window.handleResetPassword = async function () {
        const p1 = ((document.getElementById('rpPass1') || {}).value || '');
        const p2 = ((document.getElementById('rpPass2') || {}).value || '');
        if (p1.length < 8 || !/[A-Za-z]/.test(p1) || !/[0-9]/.test(p1)) {
            toastMsg(t('الباسورد لازم ٨ حروف على الأقل ويحتوي على حروف وأرقام', 'Password must be 8+ chars with letters and numbers'));
            return;
        }
        if (p1 !== p2) { toastMsg(t('الباسورد مش متطابق', 'Passwords do not match')); return; }
        let email = '', token = '';
        try {
            const q = new URLSearchParams(location.search);
            email = q.get('email') || '';
            token = q.get('t') || '';
        } catch (e) {}
        const clearResetParams = () => { try { history.replaceState(history.state, '', '/reset-password'); } catch (e) {} };
        if (!email || !token) {
            toastMsg(t('الرابط غير صالح — اطلب رابط استعادة جديد', 'Invalid link — request a new reset link'));
            clearResetParams();
            navigate('forgot-password');
            return;
        }
        try {
            await API.fetch('/auth/reset-password', { method: 'POST', noAuth: true, body: { email: email, token: token, newPassword: p1 } });
            toastMsg(t('تم تغيير كلمة السر بنجاح — سجل دخولك', 'Password changed — please sign in'));
            clearResetParams(); // التوكن لا يبقى في الـ URL بعد الاستخدام
            navigate('login');
        } catch (e) {
            toastMsg(e.message || t('انتهت صلاحية الرابط — اطلب رابط جديد', 'Link expired — request a new one'));
        }
    };

    window.handleLogin = async function () {
        if (typeof validateLoginForm === 'function' && !validateLoginForm()) {
            toastMsg(t('برجاء تصحيح البيانات الموضحة بالأحمر', 'Please fix the highlighted fields'));
            return;
        }
        const user = (document.getElementById('loginUser') || {}).value;
        const pass = (document.getElementById('loginPass') || {}).value;
        try {
            const data = await API.fetch('/auth/login', { method: 'POST', noAuth: true, body: { email: (user || '').trim(), password: pass } });
            API.setToken(data.accessToken);
            isLoggedIn = true;
            try { sessionStorage.setItem('remal_loggedIn', 'true'); } catch (e) {}
            await mergeGuestCart();
            await syncWishlistFromServer();
            // Pull saved address from server so future checkouts autofill
            await loadSavedShippingFromServer();
            updateCartBadge(); renderCartDrawer(); updateWishlistBadge();
            if (typeof updateAuthUI === 'function') updateAuthUI();
            const firstName = (data.user && data.user.fullName) ? data.user.fullName.split(' ')[0] : '';
            toastMsg(firstName ? t('أهلاً بيك ' + firstName + ' 🤍', 'Welcome back, ' + firstName + ' 🤍')
                               : t('أهلاً بيك 🤍', 'Welcome back 🤍'));
            navigate('home');
        } catch (e) { toastMsg(e.message); }
    };
    window.handleRegister = async function () {
        if (typeof validateRegisterForm === 'function' && !validateRegisterForm()) {
            toastMsg(t('برجاء تصحيح البيانات الموضحة بالأحمر', 'Please fix the highlighted fields'));
            return;
        }
        const v = id => (document.getElementById(id) || {}).value || '';
        const pass = v('regPassword'), confirm = v('regConfirmPass');
        if (pass !== confirm) { alert(t('الباسورد مش متطابق', 'Passwords do not match')); return; }
        const fullName = (v('regFirstName') + ' ' + v('regLastName')).trim();
        try {
            await API.fetch('/auth/register', { method: 'POST', noAuth: true, body: {
                email: v('regEmail').trim(), fullName: fullName, phone: v('regPhone').trim(), password: pass
            } });
            // تتبع: حساب جديد — جمهور أول يقدر يتعمل عليه Lookalike بعدين
            try {
                window.RemalTrack.identify({
                    email: v('regEmail').trim(), phone: v('regPhone').trim(),
                    firstName: (fullName || '').split(' ')[0],
                    lastName: (fullName || '').split(' ').slice(1).join(' '),
                    externalId: v('regEmail').trim()
                });
                window.RemalTrack.event('sign_up', { method: 'email' });
            } catch (e) {}
            // auto login
            const data = await API.fetch('/auth/login', { method: 'POST', noAuth: true, body: { email: v('regEmail').trim(), password: pass } });
            API.setToken(data.accessToken);
            isLoggedIn = true;
            try { sessionStorage.setItem('remal_loggedIn', 'true'); } catch (e) {}
            await mergeGuestCart();
            await loadSavedShippingFromServer();
            updateCartBadge(); renderCartDrawer();
            if (typeof updateAuthUI === 'function') updateAuthUI();
            const firstName = v('regFirstName').trim();
            toastMsg(firstName ? t('أهلاً بيك ' + firstName + ' — كسبت 100 نقطة 🤍', 'Welcome ' + firstName + ' — you earned 100 points 🤍')
                               : t('تم إنشاء الحساب وكسبت 100 نقطة 🤍', 'Account created — you earned 100 points 🤍'));
            navigate('home');
        } catch (e) { toastMsg(e.message); }
    };
    window.handleLogout = async function () {
        try { await API.fetch('/auth/logout', { method: 'POST' }); } catch (e) {}
        API.setToken(null);
        isLoggedIn = false;
        try { sessionStorage.removeItem('remal_loggedIn'); } catch (e) {}
        cart = [];
        saveGuestCart();
        updateCartBadge(); renderCartDrawer();
        if (typeof updateAuthUI === 'function') updateAuthUI();
        navigate('home');
    };
    window.saveProfile = async function () {
        const v = id => (document.getElementById(id) || {}).value || '';
        try {
            await API.fetch('/auth/me', { method: 'PUT', body: {
                fullName: (v('profFirstName') + ' ' + v('profLastName')).trim(),
                phone: v('profPhone'), city: null, birthday: null
            } });
            toastMsg(t('تم حفظ التغييرات', 'Changes saved'));
        } catch (e) { alert(e.message); }
    };
    async function syncWishlistFromServer() {
        try {
            const items = await API.fetch('/wishlist');
            wishlist = (items || []).map(w => ({ productId: w.productId, name: w.productName, nameEn: w.productName, price: w.minPrice, img: w.imageUrl, volume: '50ML' }));
            saveWishlist();
        } catch (e) {}
    }

    // -------- Bug 3 fix: wishlist drawer renders with data-product-id, handler reads from DOM, not closure --------
    // The original `addFromWishlist(${idx})` captured the row's INDEX at render time. If wishlist
    // mutated (remove, sync, re-render) the index would refer to a different row → wrong product
    // added to cart. We now use the productId directly so the binding is stable forever.
    window.renderWishlistDrawer = function () {
        const body = document.getElementById('wishlistDrawerBody');
        const countEl = document.getElementById('wishlistDrawerCount');
        if (!body) return;
        if (countEl) countEl.textContent = wishlist.length;
        if (wishlist.length === 0) {
            body.innerHTML = '<div class="wishlist-empty">'
                + '<svg viewBox="0 0 24 24"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>'
                + '<h3>' + t('قائمة أمنياتك فارغة', 'Your wishlist is empty') + '</h3>'
                + '<p>' + t('اضغط على القلب في أي عطر يعجبك', 'Tap the heart on any fragrance you love') + '</p>'
                + '</div>';
            wireWishlistDrawerHandlers();
            return;
        }
        body.innerHTML = wishlist.map(item => {
            const pid = String(item.productId || '');
            const display = (document.documentElement.dir === 'rtl') ? item.name : (item.nameEn || item.name);
            return '<div class="wishlist-item">'
                + '<img loading="lazy" decoding="async" src="' + esc(item.img || '') + '" alt="' + esc(display) + '">'
                + '<div class="wishlist-item-info">'
                +   '<div class="wishlist-item-name">' + esc(display) + '</div>'
                +   '<div class="wishlist-item-price en-num">' + Number(item.price || 0).toLocaleString('en-US') + ' ' + t('ج.م', 'EGP') + '</div>'
                +   '<div class="wishlist-item-actions">'
                +     '<button class="wishlist-add-btn" type="button" data-action="wl-add" data-product-id="' + esc(pid) + '">' + t('أضِف إلى الحقيبة', 'ADD TO BAG') + '</button>'
                +     '<button class="wishlist-remove-btn" type="button" data-action="wl-remove" data-product-id="' + esc(pid) + '" title="' + t('حذف', 'Remove') + '">'
                +       '<svg viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/></svg>'
                +     '</button>'
                +   '</div>'
                + '</div>'
                + '</div>';
        }).join('');
        wireWishlistDrawerHandlers();
    };
    function wireWishlistDrawerHandlers() {
        const root = document.getElementById('wishlistDrawer');
        if (!root || root.dataset.wired === '1') return;
        root.dataset.wired = '1';
        root.addEventListener('click', function (e) {
            const btn = e.target.closest('[data-action]');
            if (!btn) return;
            const action = btn.getAttribute('data-action');
            const pid = btn.getAttribute('data-product-id');
            if (!pid) return;
            if (action === 'wl-add') addFromWishlistById(pid);
            else if (action === 'wl-remove') removeFromWishlistById(pid);
        });
    }
    window.addFromWishlistById = function (pid) {
        const item = wishlist.find(w => String(w.productId) === String(pid));
        if (!item) { toastMsg(t('المنتج غير موجود', 'Product not found')); return; }
        // Look up the live product to use its actual default size + price (the wishlist row's cached price may be stale)
        const p = productMap[pid];
        const ds = p ? defaultSize(p) : { volume: item.volume || '50ML', price: item.price || 0 };
        addProductToCart({
            productId: pid,
            volume: ds.volume,
            price: ds.price,
            name: (p && p.name) || item.name,
            nameEn: (p && p.nameEn) || item.nameEn,
            img: (p && p.imageUrl) || item.img,
            qty: 1
        });
    };
    window.removeFromWishlistById = async function (pid) {
        const idx = wishlist.findIndex(w => String(w.productId) === String(pid));
        if (idx < 0) return;
        wishlist.splice(idx, 1);
        if (API.isAuthed()) {
            try { await API.fetch('/wishlist/' + pid, { method: 'DELETE' }); }
            catch (e) { toastMsg(e.message); /* keep local state */ }
        }
        saveWishlist();
        updateWishlistBadge();
        renderWishlistDrawer();
        if (typeof renderAccountWishlist === 'function') renderAccountWishlist();
        syncHeartIcons && syncHeartIcons();
        toastMsg(t('تم الحذف من المفضلة', 'Removed from wishlist'));
    };
    // Keep the old name working in case any inline onclick still references it
    window.addFromWishlist = function (arg) {
        if (typeof arg === 'string' && arg.length > 8) return addFromWishlistById(arg);
        const item = wishlist[arg];
        if (item && item.productId) return addFromWishlistById(item.productId);
    };
    window.removeFromWishlist = function (arg) {
        if (typeof arg === 'string' && arg.length > 8) return removeFromWishlistById(arg);
        const item = wishlist[arg];
        if (item && item.productId) return removeFromWishlistById(item.productId);
    };

    // ---- account ----
    async function loadAccount() {
        try {
            const me = await API.fetch('/auth/me');
            const parts = (me.fullName || '').split(' ');
            const setVal = (id, val) => { const el = document.getElementById(id); if (el) el.value = val || ''; };
            setVal('profFirstName', parts[0] || '');
            setVal('profLastName', parts.slice(1).join(' '));
            setVal('profPhone', me.phone);
            setVal('profEmail', me.email);
        } catch (e) {}
        loadLoyalty();
        loadMyOrders();
    }
    async function loadLoyalty() {
        try {
            const bal = await API.fetch('/loyalty/balance');
            // الرقم لوحده، والوحدة في سطر منفصل (.points-balance-unit) عشان الترجمة تشتغل
            const balEl = document.querySelector('#tab-points .points-balance');
            if (balEl) balEl.textContent = (bal.balance || 0).toLocaleString('en-US');
            const fill = document.querySelector('#tab-points .points-progress-fill');
            if (fill) {
                const tierMax = { 'رملة': 500, 'تل رملي': 1500, 'كثيب': 3000, 'صحراء': 3000 };
                const pct = Math.min(100, ((bal.balance || 0) / (tierMax[bal.tierName] || 3000)) * 100);
                fill.style.width = pct + '%';
            }
            // سطر المستوى: نكتب النص للّغتين في data-ar/data-en كمان عشان زرار اللغة
            // يقدر يبدّله فورًا من غير ريفريش (كان نص ثابت وقت الرسم).
            const cur2 = document.querySelector('#tab-points .points-current-tier');
            if (cur2) {
                const arTxt = bal.pointsToNextTier
                    ? ('مستواك: ' + bal.tierName + ' — باقي ' + bal.pointsToNextTier.toLocaleString('en-US') + ' نقطة للوصول إلى ' + bal.nextTierName)
                    : ('مستواك: ' + bal.tierName + ' — وصلت لأعلى مستوى');
                const enTxt = bal.pointsToNextTier
                    ? ('Tier: ' + bal.tierName + ' — ' + bal.pointsToNextTier.toLocaleString('en-US') + ' points to ' + bal.nextTierName)
                    : ('Tier: ' + bal.tierName + ' — highest tier reached');
                cur2.classList.add('lang-text');
                cur2.setAttribute('data-ar', arTxt);
                cur2.setAttribute('data-en', enTxt);
                cur2.textContent = isRtl() ? arTxt : enTxt;
            }
            const tx = await API.fetch('/loyalty/transactions');
            const tbody = document.querySelector('#tab-points .points-history-table tbody');
            if (tbody) {
                tbody.innerHTML = (tx || []).map(x =>
                    '<tr><td class="en-num">' + new Date(x.timestamp).toLocaleDateString('en-GB') + '</td>'
                    + '<td>' + esc(x.description) + '</td>'
                    + '<td class="' + (x.points >= 0 ? 'points-earned' : 'points-spent') + '">' + (x.points >= 0 ? '+' : '') + x.points + '</td></tr>'
                ).join('') || '<tr><td colspan="3" style="text-align:center;color:var(--text-muted);">' + t('لا توجد حركات', 'No transactions') + '</td></tr>';
            }
        } catch (e) {}
    }
    async function loadMyOrders() {
        const list = document.getElementById('ordersList');
        if (!list) return;
        let codes = [];
        try { codes = JSON.parse(localStorage.getItem('remal_my_orders') || '[]'); } catch (e) {}
        if (!codes.length) {
            list.innerHTML = '<div style="text-align:center;padding:30px;color:var(--text-muted);">' + t('لا توجد طلبات بعد', 'No orders yet') + '</div>';
            return;
        }
        list.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted);">' + t('بنحمّل طلباتك...', 'Loading your orders...') + '</div>';
        const orders = await Promise.all(codes.map(c => API.fetch('/orders/by-code/' + encodeURIComponent(c), { noAuth: true }).catch(() => null)));
        const valid = orders.filter(Boolean);
        if (!valid.length) { list.innerHTML = '<div style="text-align:center;padding:30px;color:var(--text-muted);">' + t('لا توجد طلبات', 'No orders') + '</div>'; return; }
        list.innerHTML = valid.map(o =>
            '<div class="order-card order-card-detailed"><div class="order-head"><div class="left">'
            + '<div class="order-id en-num">' + esc(o.code) + '</div>'
            + '<div class="order-date en-num">' + new Date(o.placedAt).toLocaleString('en-GB') + '</div></div>'
            + '<span class="order-status">' + t(STATUS_AR[o.status] || o.status, STATUS_EN[o.status] || o.status) + '</span></div>'
            + '<div class="order-items-grid">' + (o.items || []).map(it =>
                '<div class="order-item-line"><img loading="lazy" decoding="async" src="' + esc(imgUrl(it.imageUrl, 200)) + '" alt="">'
                + '<div style="flex:1;min-width:0;"><div class="nm">' + esc(it.itemName) + '</div>'
                + '<div class="meta"><span class="en-num">' + esc(dispVol(it.volume || '')) + '</span><span class="qty en-num">×' + it.quantity + '</span></div></div>'
                + '<div class="pr en-num">' + money(it.lineTotal) + ' ' + cur() + '</div></div>'
            ).join('') + '</div>'
            + '<div class="order-foot"><div class="totals"><span class="k">' + t('الإجمالي', 'Total') + '</span>'
            + '<span class="v en-num">' + money(o.total) + ' ' + cur() + '</span></div>'
            + '<button class="btn-track-sm" onclick="trackOrderById(\'' + esc(o.code) + '\')">' + t('تتبع الطلب', 'Track Order') + '</button></div></div>'
        ).join('');
    }

    // ---- Bug 6: language toggle re-renders every Phase-2 dynamic surface ----
    // The original toggleLanguage() swaps static .lang-text + .data-placeholder-* elements
    // and re-renders the cart drawer. But dynamic content rendered via Phase 2 (home grids,
    // catalog, bundles, collections, product-detail, wishlist drawer, account, tracking)
    // bakes the current-lang strings in at render time via t(ar,en). We re-run every relevant
    // renderer so the language flip propagates everywhere.
    const _origToggleLang = window.toggleLanguage;
    window.toggleLanguage = function () {
        if (typeof _origToggleLang === 'function') _origToggleLang();
        const active = document.querySelector('.page-section.active');
        try {
            if (active) {
                if (active.id === 'home' && typeof renderHome === 'function') renderHome();
                else if (active.id === 'perfumes' && typeof renderCatalog === 'function') renderCatalog(currentSearchQuery ? '&search=' + encodeURIComponent(currentSearchQuery) : '');
                else if (active.id === 'bundles' && typeof renderBundlesInto === 'function') {
                    const bg = document.getElementById('bundlesGrid');
                    if (bg) { bg.dataset.loaded = '1'; renderBundlesInto(['bundlesGrid']); }
                }
                else if (active.id === 'collections' && typeof renderCollectionsInto === 'function') {
                    const cg = document.getElementById('collectionsPageGrid');
                    if (cg) { cg.dataset.loaded = '1'; renderCollectionsInto(['collectionsPageGrid']); }
                }
                else if (active.id === 'product-detail' && typeof currentProductId !== 'undefined' && productMap[currentProductId] && typeof renderProductDetail === 'function') {
                    renderProductDetail(productMap[currentProductId]);
                }
                else if (active.id === 'account' && typeof loadAccount === 'function' && API.isAuthed()) {
                    loadAccount();
                }
            }
            // Drawers
            if (typeof renderWishlistDrawer === 'function') renderWishlistDrawer();
            if (typeof renderCartDrawer === 'function') renderCartDrawer();
            // الماركي السفلي — أعد رسمه باللغة الجديدة
            const hm = document.getElementById('homeMarquee');
            if (hm && !hm.hidden && typeof renderHomeMarquee === 'function') {
                renderHomeMarquee(hm.dataset.ar, hm.dataset.en);
            }
            // Saved-info banner text
            const banner = document.getElementById('savedInfoBanner');
            if (banner) { banner.remove(); if (typeof ensureSavedInfoBanner === 'function') ensureSavedInfoBanner(); }
            // شريط الإعلانات — أعد رسم الرسالة الحالية باللغة الجديدة
            if (typeof window._repaintAnnouncement === 'function') window._repaintAnnouncement();
        } catch (e) { console.warn('toggleLanguage re-render failed', e); }
    };

    // ---- navigate wrapper (with browser-history integration) ----
    const _navPrev = window.navigate;
    function _runNavSideEffects(pageId, arg) {
        if (pageId !== 'perfumes' && typeof clearSearchState === 'function') clearSearchState();
        // ===== ختم اللغة لكل صفحة =====
        // كروت المنتجات/الباقات/المجموعات مبنية بالـ innerHTML على اللغة وقت الرسم.
        // تبديل اللغة كان بيعيد رسم **الصفحة النشطة فقط**، فأي صفحة اتفتحت قبل كده
        // (الرئيسية مثلاً، اللي بترسم مرة واحدة عند الإقلاع) بتفضل باللغة القديمة لما
        // ترجعلها. الحل: نختم كل صفحة باللغة اللي اترسمت بيها، ولو الختم مختلف عن
        // اللغة الحالية نعيد رسمها عند الزيارة.
        const lang = (document.documentElement.getAttribute('lang') === 'en') ? 'en' : 'ar';
        const sec = document.getElementById(pageId);
        const staleLang = !!(sec && sec.dataset.rlang && sec.dataset.rlang !== lang);
        if (sec) sec.dataset.rlang = lang;

        if (pageId === 'home') {
            if (staleLang && typeof renderHome === 'function') renderHome();
        } else if (pageId === 'all-products') {
            // نرسم كل زيارة (الكاش بيمنع أي طلب شبكة زيادة)
            if (typeof renderAllProductsPage === 'function') renderAllProductsPage();
        } else if (pageId === 'perfumes') {
            const cg = document.getElementById('catalogGrid');
            if (cg && (!cg.dataset.loaded || staleLang)) { cg.dataset.loaded = '1'; renderCatalog(''); }
        } else if (pageId === 'bundles') {
            // نرسم من جديد كل زيارة (نسخة السيرفر من الكاش) عشان ميظهرش أي placeholder ثابت.
            if (typeof renderBundlesFullPage === 'function') renderBundlesFullPage();
        } else if (pageId === 'collections') {
            if (typeof renderCollectionsFullPage === 'function') renderCollectionsFullPage();
        } else if (pageId === 'product-detail') {
            if (staleLang && typeof currentProductId !== 'undefined' && currentProductId && typeof openProductDetail === 'function') openProductDetail(currentProductId);
        } else if (pageId === 'collection-detail') {
            if (staleLang && typeof currentCollectionId !== 'undefined' && currentCollectionId && typeof openCollectionDetail === 'function') openCollectionDetail(currentCollectionId);
        } else if (pageId === 'bundle-detail') {
            if (staleLang && typeof currentBundleId !== 'undefined' && currentBundleId && typeof openBundleDetail === 'function') openBundleDetail(currentBundleId);
        } else if (pageId === 'account') {
            if (!API.isAuthed()) { _navPrev('login'); return; }
            loadAccount();
        }
        document.dispatchEvent(new CustomEvent('remal:navigated', { detail: pageId }));
    }

    // History integration: each navigate() pushes a state entry; the browser's Back/Forward
    // buttons then move WITHIN the SPA instead of exiting the website.
    // Clean URLs: we push real paths (/perfumes, /bundles ...) بدلاً من #hash —
    // الباك إند يعيد remal.html لأي مسار غير معروف (MapFallbackToFile) فتعمل الروابط المباشرة.
    // Also persist current page to sessionStorage so an F5 refresh lands on the same page.
    // صفحات التفاصيل بتاخد رابط العنصر نفسه (/product/{id} و /bundle/{id} و /collection/{id})
    // — نفس الروابط اللي في sitemap.xml — عشان كل عطر وكل باقة يبقى له canonical مستقل
    // بدل ما كل المنتجات تتشارك رابط واحد (/product-detail) في نتائج البحث.
    function _cleanPathFor(pageId) {
        if (pageId === 'home') return '/';
        try {
            if (pageId === 'product-detail' && typeof currentProductId !== 'undefined' && currentProductId)
                return '/product/' + currentProductId;
            if (pageId === 'bundle-detail' && typeof currentBundleId !== 'undefined' && currentBundleId)
                return '/bundle/' + currentBundleId;
            if (pageId === 'collection-detail' && typeof currentCollectionId !== 'undefined' && currentCollectionId)
                return '/collection/' + currentCollectionId;
        } catch (e) {}
        return '/' + pageId;
    }
    window.navigate = function (pageId, arg) {
        _navPrev(pageId);
        // حدّث الـ URL أولاً حتى يقرأ مستمعا الـ canonical/GA (remal:navigated) المسار الجديد
        const path = _cleanPathFor(pageId);
        if (location.pathname !== path || location.hash) {
            try { history.pushState({ pageId: pageId, arg: arg, ramal: 1 }, '', path + location.search); } catch (e) {}
        }
        _runNavSideEffects(pageId, arg);
        // تمرير سلس لأعلى الصفحة عند التنقل (بدل القفزة المفاجئة)
        try { window.scrollTo({ top: 0, behavior: 'smooth' }); } catch (e) { window.scrollTo(0, 0); }
        // Persist for refresh-recovery
        try {
            sessionStorage.setItem('remal_page', pageId);
            if (pageId === 'product-detail' && typeof currentProductId !== 'undefined' && currentProductId) {
                sessionStorage.setItem('remal_product_id', currentProductId);
            } else if (pageId === 'collection-detail' && typeof currentCollectionId !== 'undefined' && currentCollectionId) {
                sessionStorage.setItem('remal_collection_id', currentCollectionId);
            } else if (pageId === 'bundle-detail' && typeof currentBundleId !== 'undefined' && currentBundleId) {
                sessionStorage.setItem('remal_bundle_id', currentBundleId);
            }
        } catch (e) {}
    };

    // Initial state — replace (don't push) so the user can never "Back" out of the site on first load.
    // On refresh: restore from the clean path (/perfumes), then legacy #hash links, then sessionStorage.
    (function restoreInitialPage() {
        let initialPage = 'home';
        try {
            let fromPath = (location.pathname || '/').replace(/^\/+|\/+$/g, '');
            // الملف نفسه (remal.html) أو الجذر = الصفحة الرئيسية
            if (fromPath === 'remal.html' || fromPath === 'index.html') fromPath = '';
            // روابط SEO المباشرة: /product/{id} و /bundle/{id} و /collection/{id} (كلها في sitemap.xml)
            const prodM = fromPath.match(/^product\/([0-9a-f-]{36})$/i);
            const collM = fromPath.match(/^collection\/([0-9a-f-]{36})$/i);
            const bundM = fromPath.match(/^bundle\/([0-9a-f-]{36})$/i);
            if (prodM) { window._pendingDeepLink = { type: 'product', id: prodM[1] }; fromPath = 'perfumes'; }
            else if (collM) { window._pendingDeepLink = { type: 'collection', id: collM[1] }; fromPath = 'collections'; }
            else if (bundM) { window._pendingDeepLink = { type: 'bundle', id: bundM[1] }; fromPath = 'bundles'; }
            const fromHash = (location.hash || '').replace(/^#/, ''); // دعم الروابط القديمة بالـ #
            const saved = sessionStorage.getItem('remal_page');
            initialPage = fromPath || fromHash || saved || 'home';
            // لو الصفحة غير معروفة (مسار غريب) نعود للرئيسية بدون كسر
            if (!document.getElementById(initialPage)) initialPage = 'home';
            history.replaceState({ pageId: initialPage, ramal: 1 }, '', _cleanPathFor(initialPage) + location.search);
        } catch (e) {}
        // شيل إخفاء الرئيسية (سكريبت منع الوميض في الـ head) لو الوجهة النهائية هي الرئيسية
        const _preRoute = document.getElementById('preRouteStyle');
        if ((!initialPage || initialPage === 'home') && _preRoute) _preRoute.remove();
        if (!initialPage || initialPage === 'home') return;
        // Run AFTER all phase scripts have wired window.navigate / openProductDetail / etc.
        const restore = function () {
            try {
                // بعد ما الراوتر يوصل للصفحة الصحيحة، شيل إخفاء الرئيسية (منع الوميض)
                setTimeout(() => { const pr = document.getElementById('preRouteStyle'); if (pr) pr.remove(); }, 120);
                // روابط SEO المباشرة (/product/{id} أو /collection/{id}) من الـ sitemap
                if (window._pendingDeepLink) {
                    const dl = window._pendingDeepLink;
                    window._pendingDeepLink = null;
                    if (dl.type === 'product' && typeof openProductDetail === 'function') { openProductDetail(dl.id); return; }
                    if (dl.type === 'collection' && typeof openCollectionDetail === 'function') { openCollectionDetail(dl.id); return; }
                    if (dl.type === 'bundle' && typeof openBundleDetail === 'function') { openBundleDetail(dl.id); return; }
                }
                if (initialPage === 'product-detail') {
                    const pid = sessionStorage.getItem('remal_product_id');
                    if (pid && typeof openProductDetail === 'function') { openProductDetail(pid); return; }
                } else if (initialPage === 'collection-detail') {
                    const cid = sessionStorage.getItem('remal_collection_id');
                    if (cid && typeof openCollectionDetail === 'function') { openCollectionDetail(cid); return; }
                } else if (initialPage === 'bundle-detail') {
                    const bid = sessionStorage.getItem('remal_bundle_id');
                    if (bid && typeof openBundleDetail === 'function') { openBundleDetail(bid); return; }
                }
                if (typeof navigate === 'function') navigate(initialPage);
            } catch (e) { /* swallow */ }
        };
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => setTimeout(restore, 30));
        } else {
            setTimeout(restore, 30);
        }
    })();

    window.addEventListener('popstate', function (e) {
        // Only handle our own state entries (avoid hijacking other libraries' history use)
        const st = e.state;
        let fromUrl = (location.pathname || '/').replace(/^\/+|\/+$/g, '');
        if (fromUrl === 'remal.html' || fromUrl === 'index.html') fromUrl = '';
        const pageId = (st && st.ramal && st.pageId) || fromUrl || (location.hash || '#home').replace(/^#/, '') || 'home';
        // Re-render without pushing another entry
        _navPrev(pageId);
        _runNavSideEffects(pageId, st && st.arg);
        try { sessionStorage.setItem('remal_page', pageId); } catch (e2) {}
    });

    // ---- session expiry ----
    window.addEventListener('remal:auth-expired', function () {
        isLoggedIn = false;
        if (typeof updateAuthUI === 'function') updateAuthUI();
        toastMsg(t('انتهت الجلسة — سجّل الدخول تاني', 'Session expired — please sign in again'));
    });

    // ---- boot ----
    // لو المستخدم عمل ريفريش وهو في صفحة الدفع، السلة (خصوصاً سلة السيرفر) بتتحمّل
    // بعد ما الصفحة اترسمت — فنعيد مزامنة الدفع بمجرد جاهزية السلة كي لا تبان فاضية.
    function _resyncCheckoutIfActive() {
        const co = document.getElementById('checkout');
        if (co && co.classList.contains('active') && typeof window.syncCheckoutFromCart === 'function') {
            window.syncCheckoutFromCart(true);
        }
    }
    function boot() {
        // مهم: الرسم لا ينتظر أي شبكة. أي منطق شبكة (تجديد الجلسة) بيتعمل في الخلفية بعد
        // ما الصفحة اترسمت، عشان المنتجات الحقيقية تحلّ محل الـ placeholder فورًا ولا تظهر
        // بيانات وهمية لو الشبكة بطيئة. وكمان تطبيق اللغة بيتم بعد تهيئة السلة (مش قبلها)
        // لأن تبديل اللغة بيعيد رسم السلة — لو اتنادى بدري بيرمي استثناء ويوقف الـ boot كله.
        const wasAuthedAtStart = API.isAuthed();
        isLoggedIn = wasAuthedAtStart;
        if (typeof updateAuthUI === 'function') updateAuthUI();

        // الهيرو فورًا من الكاش (قبل رد الـ API): الزيارات المتكررة تشوف صور الهيرو الصح
        // من أول فريم بدل التدرّج المحايد — و loadSettings يحدّثها بس لو اتغيّرت من الداشبورد.
        try {
            const cachedHero = JSON.parse(localStorage.getItem('remal_hero_slides') || '[]');
            if (Array.isArray(cachedHero) && cachedHero.length && typeof initHeroCarousel === 'function') {
                initHeroCarousel(cachedHero);
                window._heroAppliedKey = JSON.stringify(cachedHero);
            }
        } catch (e) {}
        // القسم الترويجي كذلك من الكاش عشان ما يظهرش متأخر ويزحزح الصفحة
        try {
            const cachedPromo = localStorage.getItem('remal_promo_section');
            if (cachedPromo) renderPromoSpotlight(cachedPromo);
        } catch (e) {}

        loadSettings();
        if (wasAuthedAtStart) {
            refreshServerCart().then(() => { updateCartBadge(); renderCartDrawer(); _resyncCheckoutIfActive(); }).catch(() => {});
            syncWishlistFromServer().then(() => updateWishlistBadge());
            // Pull saved shipping into sessionStorage so the checkout banner can offer autofill
            if (typeof loadSavedShippingFromServer === 'function') loadSavedShippingFromServer().catch(() => {});
        } else {
            cart = loadGuestCart();
            updateCartBadge(); renderCartDrawer(); _resyncCheckoutIfActive();
        }

        // طبّق اللغة على النصوص بعد تهيئة الحالة. الأولوية: اختيار الزائر ← اللغة الافتراضية
        // اللي حددها الأدمن (مكاشية) ← العربية.
        // مهم: سكربت الـ head بيظبط lang/dir بس — الماركب نفسه مكتوب بالعربي، والنصوص
        // مابتتترجمش غير من applyLanguage. من غير السطور دي كان الموقع يفتح بـ
        // lang="en" dir="ltr" وكل النصوص عربي لما الأدمن يخلي الافتراضي إنجليزي.
        try {
            const savedLang = localStorage.getItem(LANG_KEY);
            const hasChoice = (savedLang === 'en' || savedLang === 'ar');
            const adminDefault = localStorage.getItem('remal_default_lang');
            const effective = hasChoice ? savedLang
                : ((adminDefault === 'en' || adminDefault === 'ar') ? adminDefault : null);
            if (effective) {
                applyLanguage(effective);
                // الزائر ما اختارش بنفسه → نسيب remal_lang فاضي عشان يفضل تابع لأي تغيير
                // مستقبلي من الأدمن (applyLanguage بتحفظ الاختيار فبنشيله).
                if (!hasChoice) localStorage.removeItem(LANG_KEY);
            }
        } catch (e) {}

        renderHome();
        wireSearch();

        // استعادة الجلسة الصامتة في الخلفية (لا تعطّل الرسم إطلاقًا): لو مفيش access token
        // لكن فيه جلسة سابقة نجرّب /auth/refresh بالكوكي (7 أيام HttpOnly)، وبعد النجاح نزامن
        // بيانات الحساب. الفشل يسيبنا زائرين بدون أي تعطيل.
        (async () => {
            if (wasAuthedAtStart) return; // اتعامل معاه فوق بالفعل
            // مهم: SESS_HINT_KEY و doRefresh خاصّين بالـ IIFE بتاع الـ API (سكربت بلوك تاني)
            // فمش مرئيين هنا — نستخدم المفتاح كنص مباشر و API.refresh المُصدَّر.
            let hadSession = false;
            try { hadSession = localStorage.getItem('remal_has_session') === '1'; } catch (e) {}
            if (!hadSession) return;
            try { await API.refresh(); }
            catch (e) { try { localStorage.removeItem('remal_has_session'); } catch (e2) {} return; }
            if (!API.isAuthed()) return;
            isLoggedIn = true;
            if (typeof updateAuthUI === 'function') updateAuthUI();
            refreshServerCart().then(() => { updateCartBadge(); renderCartDrawer(); _resyncCheckoutIfActive(); }).catch(() => {});
            syncWishlistFromServer().then(() => updateWishlistBadge());
            if (typeof loadSavedShippingFromServer === 'function') loadSavedShippingFromServer().catch(() => {});
        })();
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})();
