    // ================= 0. Cart State =================
    let cart = [];

    // ================= 1. التنقل بين الصفحات (SPA) =================
    function navigate(pageId) {
        document.querySelectorAll('.page-section').forEach(s => s.classList.remove('active'));
        const targetPage = document.getElementById(pageId);
        if(targetPage) targetPage.classList.add('active');

        // Fix active link - compare by pageId
        // ملاحظة: روابط القائمة الجانبية أزرار بكلاس .mnav-link (مش <a> جوه .mobile-nav)،
        // فكانت مستثناة من المطابقة وحالة "نشط" ما كانتش بتظهر فيها أبدًا.
        document.querySelectorAll('.nav-links a, .mobile-nav a, .mnav-link').forEach(l => {
            l.classList.remove('active-link');
            const onclick = l.getAttribute('onclick') || '';
            if(onclick.includes(`'${pageId}'`) || onclick.includes(`"${pageId}"`)) l.classList.add('active-link');
        });

        // Close mobile nav drawer
        const mobileNav = document.getElementById('mobileNav');
        const mobileNavOverlay = document.getElementById('mobileNavOverlay');
        const menuToggle = document.getElementById('menuToggle');
        if(mobileNav) mobileNav.classList.remove('open');
        if(mobileNavOverlay) mobileNavOverlay.classList.remove('open');
        if(menuToggle) menuToggle.classList.remove('open');
        document.body.style.overflow = '';

        // Close cart drawer if open
        closeCartDrawer();

        // Close wishlist drawer if open
        const wd = document.getElementById('wishlistDrawer');
        const wo = document.getElementById('wishlistOverlay');
        if(wd) wd.classList.remove('open');
        if(wo) wo.classList.remove('open');

        window.scrollTo({ top: 0, behavior: 'smooth' });

        // Update navbar state after navigating
        setTimeout(updateNavbar, 50);
    }

    function toggleMenu() {
        const drawer = document.getElementById('mobileNav');
        const overlay = document.getElementById('mobileNavOverlay');
        const menuToggle = document.getElementById('menuToggle');
        if(!drawer || !overlay) return;
        const isOpen = drawer.classList.contains('open');
        drawer.classList.toggle('open');
        overlay.classList.toggle('open');
        if(menuToggle) menuToggle.classList.toggle('open');
        document.body.style.overflow = isOpen ? '' : 'hidden';
    }

    function toggleMnavSub(menuId, btn) {
        const menu = document.getElementById(menuId);
        if(!menu) return;
        const isOpen = menu.classList.contains('open');
        // Close all sub menus
        document.querySelectorAll('.mnav-submenu').forEach(m => m.classList.remove('open'));
        document.querySelectorAll('.mnav-link.has-sub').forEach(b => b.classList.remove('open'));
        if(!isOpen) {
            menu.classList.add('open');
            btn.classList.add('open');
        }
    }

    function toggleAccordion(btn) {
        const item = btn.parentElement;
        const content = btn.nextElementSibling;
        if(item.classList.contains('active')) {
            item.classList.remove('active');
            content.style.maxHeight = null;
        } else {
            document.querySelectorAll('.accordion-item').forEach(acc => {
                acc.classList.remove('active');
                const c = acc.querySelector('.accordion-content');
                if(c) c.style.maxHeight = null;
            });
            item.classList.add('active');
            content.style.maxHeight = content.scrollHeight + "px";
        }
    }

    // ================= 2. اللغات =================
    const LANG_KEY = 'remal_lang';
    // يطبّق اللغة على كامل الصفحة ويحفظ الاختيار في localStorage عشان يفضل بعد الريفريش.
    function applyLanguage(newLang) {
        const html = document.documentElement;
        const newDir = newLang === 'ar' ? 'rtl' : 'ltr';
        html.setAttribute('dir', newDir);
        html.setAttribute('lang', newLang);
        document.querySelectorAll('.lang-text').forEach(el => {
            const t = el.getAttribute(`data-${newLang}`);
            if(t) el.innerHTML = t;
        });
        document.querySelectorAll('[data-placeholder-ar],[data-placeholder-en]').forEach(el => {
            const p = el.getAttribute(`data-placeholder-${newLang}`);
            if(p) el.setAttribute('placeholder', p);
        });
        const langBtn = document.querySelector('.lang-toggle');
        if(langBtn) langBtn.innerText = newLang === 'ar' ? 'EN' : 'AR';
        try { localStorage.setItem(LANG_KEY, newLang); } catch (e) {}
        // إعادة رسم العناصر المعتمِدة على اللغة — كلٌّ في try مستقل عشان أي خطأ فيها
        // (مثلاً لو السلة لسه ماتهيّأتش) ميوقفش تطبيق اللغة ولا الـ caller (زي boot).
        try { if(typeof updateTotalDisplay === 'function') updateTotalDisplay(); } catch (e) {}
        try { if(typeof renderCartDrawer === 'function') renderCartDrawer(); } catch (e) {}
        try { if(typeof syncMnavLangButtons === 'function') syncMnavLangButtons(); } catch (e) {}
        // زر جوجل داخل iframe ولغته من مكتبة GSI نفسها — نعيد تحميلها باللغة الجديدة
        try { if (typeof window.reloadGsiForLang === 'function') window.reloadGsiForLang(newLang); } catch (e) {}
        // أي واجهة تانية مبنية بالـ JS ومحتاجة إعادة ترجمة
        try { if (typeof window.refreshDynamicLangUI === 'function') window.refreshDynamicLangUI(); } catch (e) {}
    }
    function toggleLanguage() {
        const isRTL = document.documentElement.getAttribute('dir') === 'rtl';
        const newLang = isRTL ? 'en' : 'ar';
        applyLanguage(newLang);
        // كل الصفحات الغير نشطة تُعلَّم كـ"مرسومة بلغة قديمة" فتُعاد رسمها عند زيارتها.
        // من غير الخطوة دي كانت الرئيسية (اللي بترسم مرة واحدة عند الإقلاع) بتفضل
        // بالعربي لو غيّرت اللغة وإنت في صفحة تانية ورجعتلها.
        try {
            const active = document.querySelector('.page-section.active');
            document.querySelectorAll('.page-section').forEach(s => {
                if (s !== active) s.dataset.rlang = (newLang === 'en' ? 'ar' : 'en');
            });
            if (active) active.dataset.rlang = newLang;   // النشطة هتترسم دلوقتي حالًا
        } catch (e) {}
        // المحتوى الديناميكي (كروت المنتجات/الباقات/المجموعات وصفحات التفاصيل) مبني بالـ innerHTML
        // على اللغة وقت الرسم، فمش بيتبدّل مع سحب الـ lang-text لوحده — نعيد رسم الصفحة النشطة.
        if (typeof _repaintActiveStorefrontPage === 'function') _repaintActiveStorefrontPage();
        // القسم الترويجي نصوصه ديناميكية كذلك
        if (typeof window.refreshPromoSpotlightLang === 'function') window.refreshPromoSpotlightLang();
    }

    function toggleSearch() {
        const modal = document.getElementById('searchModal');
        if(!modal) return;
        modal.classList.toggle('active');
        if(modal.classList.contains('active')) {
            document.body.style.overflow = 'hidden';
            setTimeout(() => document.getElementById('searchInput').focus(), 100);
        } else {
            document.body.style.overflow = '';
        }
    }

    // ================= 3. Navbar + Ticker =================
    function updateNavbar() {
        const navbar = document.getElementById("mainNavbar");
        const ticker = document.querySelector(".sand-marquee-wrapper");
        const homeSection = document.getElementById("home");
        if(!navbar) return;
        const isHome = homeSection && homeSection.classList.contains("active");
        const atTop = window.scrollY <= 30;
        if(isHome && atTop) {
            navbar.classList.add("transparent");
            if(ticker) ticker.classList.add("ticker-hidden");
        } else {
            navbar.classList.remove("transparent");
            if(ticker) ticker.classList.remove("ticker-hidden");
        }
    }

    document.addEventListener("DOMContentLoaded", () => {
        // Navbar scroll behaviour
        window.addEventListener("scroll", updateNavbar);
        const homeSection = document.getElementById("home");
        if(homeSection) {
            new MutationObserver(updateNavbar).observe(homeSection, { attributes: true, attributeFilter: ['class'] });
        }
        updateNavbar();

        // Sticky bottom bar on product detail
        const actionRow = document.getElementById('mainActionRow');
        const stickyCart = document.getElementById('stickyCart');
        if(actionRow && stickyCart) {
            window.addEventListener('scroll', () => {
                const rect = actionRow.getBoundingClientRect();
                stickyCart.classList.toggle('visible', rect.bottom < 0);
            });
        }

        // Re-wire all mobile add buttons with bottle animation
        document.querySelectorAll('.mobile-add-btn').forEach(btn => {
            if(btn.querySelector('.bottle-cart-wrap')) return;
            btn.innerHTML = `
                <div class="btn-text-content">
                    <svg viewBox="0 0 24 24" style="width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:2;"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"></path><line x1="3" y1="6" x2="21" y2="6"></line></svg>
                    <span class="lang-text" data-ar="أضِف إلى الحقيبة" data-en="ADD TO BAG">أضِف إلى الحقيبة</span>
                </div>
                <div class="bottle-cart-wrap">
                    <svg class="bottle-icon-svg" viewBox="0 0 24 24"><rect x="7" y="10" width="10" height="12" rx="2"/><path d="M10 10V6h4v4"/><rect x="9" y="3" width="6" height="3" rx="1"/></svg>
                    <svg class="cart-icon-svg" viewBox="0 0 24 24"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"></path><line x1="3" y1="6" x2="21" y2="6"></line></svg>
                </div>
                <div class="success-content">
                    <svg viewBox="0 0 24 24" style="width:18px;height:18px;stroke:currentColor;fill:none;stroke-width:3;"><polyline points="20 6 9 17 4 12"></polyline></svg>
                </div>`;
            btn.removeAttribute('onclick');
            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                e.preventDefault();
                // Get product info from parent card
                const card = this.closest('.noon-card');
                const nameEl = card ? card.querySelector('.product-title') : null;
                const priceEl = card ? card.querySelector('.amount') : null;
                const imgEl = card ? card.querySelector('.product-img') : null;
                const volEl = card ? card.querySelector('.volume-tag') : null;
                const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
                const name = nameEl ? nameEl.innerText.trim() : (isRtl ? 'عطر رمال' : 'Remal Perfume');
                const price = priceEl ? parseInt(priceEl.innerText.replace(/,/g,'')) : 990;
                const img = imgEl ? imgEl.src : '';
                const vol = volEl ? volEl.innerText.trim() : '55 ML';
                addProductToCart({ id: Date.now(), name, nameEn: name, price, qty: 1, img, volume: vol });
                addWithBottleAnim(this);
            });
        });

        // Initial cart render
        renderCartDrawer();
        updateCartBadge();
    });

    // ================= 4. Cart Drawer =================
    function toggleCartDrawer() {
        const drawer = document.getElementById('cartDrawer');
        const overlay = document.getElementById('cartOverlay');
        if(!drawer || !overlay) return;
        const isOpen = drawer.classList.contains('open');
        if(isOpen) { closeCartDrawer(); }
        else { openCartDrawer(); }
    }

    function openCartDrawer() {
        const drawer = document.getElementById('cartDrawer');
        const overlay = document.getElementById('cartOverlay');
        if(!drawer || !overlay) return;
        renderCartDrawer();
        drawer.classList.add('open');
        overlay.classList.add('open');
        document.body.style.overflow = 'hidden';
    }

    function closeCartDrawer() {
        const drawer = document.getElementById('cartDrawer');
        const overlay = document.getElementById('cartOverlay');
        if(!drawer || !overlay) return;
        drawer.classList.remove('open');
        overlay.classList.remove('open');
        document.body.style.overflow = '';
    }

    function renderCartDrawer() {
        const body = document.getElementById('cartDrawerBody');
        const footer = document.getElementById('cartDrawerFooter');
        const countEl = document.getElementById('drawerCount');
        if(!body || !footer) return;

        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const totalQty = cart.reduce((s, i) => s + i.qty, 0);
        const subtotal = cart.reduce((s, i) => s + i.price * i.qty, 0);
        if(countEl) countEl.innerText = totalQty;

        if(cart.length === 0) {
            body.innerHTML = `<div class="cart-drawer-empty">
                <svg viewBox="0 0 24 24"><path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4z"/><line x1="3" y1="6" x2="21" y2="6"/></svg>
                <p>${isRtl ? 'حقيبتك فارغة حتى الآن' : 'Your bag is empty'}</p>
            </div>`;
            footer.innerHTML = `<button class="drawer-continue-btn" onclick="closeCartDrawer()">${isRtl ? 'اكتشف عطورنا' : 'DISCOVER FRAGRANCES'}</button>`;
            return;
        }

        body.innerHTML = cart.map(item => `
            <div class="drawer-item drawer-item-enter" id="ditem_${item.id}">
                <img loading="lazy" decoding="async" class="drawer-item-img" src="${item.img}" alt="${item.name}">
                <div class="drawer-item-info">
                    <div class="drawer-item-name">${isRtl ? item.name : (item.nameEn || item.name)}</div>
                    <div class="drawer-item-vol en-num">${window.dispVol ? window.dispVol(item.volume) : item.volume}</div>
                    <div class="drawer-item-controls">
                        <div class="drawer-qty">
                            <button class="drawer-qty-btn" onclick="updateDrawerQty('${item.id}', -1)">−</button>
                            <span class="drawer-qty-val en-num">${item.qty}</span>
                            <button class="drawer-qty-btn" onclick="updateDrawerQty('${item.id}', 1)">+</button>
                        </div>
                        <button class="drawer-remove-btn" onclick="removeFromDrawer('${item.id}')" title="${isRtl ? 'حذف' : 'Remove'}">
                            <svg viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/></svg>
                        </button>
                    </div>
                </div>
                <div class="drawer-item-price en-num">${(item.price * item.qty).toLocaleString('en-US')} <span style="font-size:11px;font-weight:600;color:#888;">${isRtl ? 'ج.م' : 'EGP'}</span></div>
            </div>`).join('');

        const freeShipping = subtotal >= 2000;
        footer.innerHTML = `
            <div class="drawer-subtotal">
                <span class="drawer-subtotal-label">${isRtl ? 'الإجمالي' : 'Subtotal'}</span>
                <span class="drawer-subtotal-amount en-num">${subtotal.toLocaleString('en-US')} <span style="font-size:13px;font-weight:600;color:#888">${isRtl ? 'ج.م' : 'EGP'}</span></span>
            </div>
            <div class="drawer-shipping-note">
                ${freeShipping
                    ? (isRtl ? '✓ مبروك! شحن مجاني على طلبك' : '✓ Congrats! Free shipping on your order')
                    : (isRtl ? `أضف ${(2000 - subtotal).toLocaleString('en-US')} ج.م عشان توصل للشحن المجاني` : `Add ${(2000 - subtotal).toLocaleString('en-US')} EGP for free shipping`)
                }
            </div>
            <button class="drawer-checkout-btn" onclick="closeCartDrawer(); navigate('checkout'); syncCheckoutFromCart();">
                <span>${isRtl ? 'إتمام الشراء' : 'CHECKOUT'}</span>
                <svg viewBox="0 0 24 24" style="width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:2.5;stroke-linecap:round;transform:${isRtl ? 'rotate(180deg)' : 'none'}"><path d="M5 12h14M12 5l7 7-7 7"/></svg>
            </button>
            <button class="drawer-continue-btn" onclick="closeCartDrawer()">${isRtl ? 'متابعة التسوق' : 'CONTINUE SHOPPING'}</button>`;
    }

    function addProductToCart(product) {
        const existing = cart.find(i => i.name === product.name);
        if(existing) { existing.qty++; }
        else { cart.push({ ...product, qty: 1 }); }
        updateCartBadge();
        renderCartDrawer();
        // Show toast
        showToast();
    }

    // ⚠️ نسخة مبكّرة — نفس المشكلة: تعديل بدون حفظ.
    function updateDrawerQty(id, delta) {
        if (window.updateDrawerQty && window.updateDrawerQty !== updateDrawerQty) {
            return window.updateDrawerQty(id, delta);
        }
        const item = cart.find(i => i.id === id);
        if(!item) return;
        item.qty += delta;
        if(item.qty <= 0) { cart = cart.filter(i => i.id !== id); }
        if (typeof saveGuestCart === 'function') saveGuestCart();       // بدونها يضيع التغيير
        updateCartBadge();
        renderCartDrawer();
    }

    // ⚠️ نسخة مبكّرة — كانت بتعدّل cart بدون حفظ فالتغيير يضيع بعد الريفريش.
    // النسخة النهائية (window.removeFromDrawer) بتتعامل مع السيرفر كذلك.
    function removeFromDrawer(id) {
        if (window.removeFromDrawer && window.removeFromDrawer !== removeFromDrawer) {
            return window.removeFromDrawer(id);
        }
        const el = document.getElementById(`ditem_${id}`);
        if(el) { el.style.opacity = '0'; el.style.transform = 'translateX(30px)'; el.style.transition = '0.25s ease'; }
        setTimeout(() => {
            cart = cart.filter(i => i.id !== id);
            if (typeof saveGuestCart === 'function') saveGuestCart();   // بدونها يرجع بعد الريفريش
            updateCartBadge();
            renderCartDrawer();
        }, 250);
    }

    function updateCartBadge() {
        const badge = document.getElementById('cartBadge');
        const total = cart.reduce((s, i) => s + i.qty, 0);
        if(badge) { badge.innerText = total; badge.style.display = total > 0 ? 'flex' : 'none'; }
    }

    function syncCheckoutFromCart() {
        // Sync cart to checkout items
        // الاسم حسب اللغة الحالية (مش اللي اتخزّن وقت الإضافة) — نفس منطق النسخة النهائية
        checkoutItems = cart.map(i => {
            const rtlNow = document.documentElement.getAttribute('dir') === 'rtl';
            return { id: i.id, price: i.price, qty: i.qty,
                     name: (rtlNow ? (i.name || i.nameEn) : (i.nameEn || i.name)) || i.name,
                     img: i.img, volume: i.volume };
        });
        // Re-render checkout cart list
        const list = document.getElementById('checkoutCartList');
        if(!list) return;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        list.innerHTML = checkoutItems.map(item => `
            <div class="cart-item" id="cartItem_${item.id}">
                <img loading="lazy" decoding="async" src="${item.img || 'https://remal-perfume.runasp.net/freshSummer.webp'}" class="cart-item-img" alt="${item.name}">
                <div class="cart-item-details">
                    <div>
                        <h3 class="cart-item-title">${item.name}</h3>
                        <div class="en-num" style="font-size:12px;color:#888;margin-top:4px;">${item.volume || '55 ML'}</div>
                    </div>
                    <div class="cart-qty-row">
                        <div class="cart-qty-controls">
                            <button class="cart-qty-btn" onclick="updateCartItemQty('${item.id}', -1)">-</button>
                            <span class="cart-qty-val en-num" id="cartItemQty_${item.id}">${item.qty}</span>
                            <button class="cart-qty-btn" onclick="updateCartItemQty('${item.id}', 1)">+</button>
                        </div>
                        <button class="cart-item-remove" onclick="removeCartItem('${item.id}')" aria-label="Remove"><svg viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/></svg></button>
                    </div>
                    <div class="cart-item-price"><span class="en-num" id="cartItemPrice_${item.id}" data-base="${item.price}">${(item.price * item.qty).toLocaleString('en-US')}</span> <span style="font-size:12px;">${isRtl ? 'ج.م' : 'EGP'}</span></div>
                </div>
            </div>`).join('');
        calculateOrderSummary();
        if (typeof window.onCheckoutShown === 'function') window.onCheckoutShown();
    }

    function showToast() {
        const toast = document.getElementById('toast');
        if(!toast) return;
        toast.classList.add('show');
        setTimeout(() => toast.classList.remove('show'), 2800);
    }

    // ================= 5. السلايدر =================
    function updateSliderDots() {
        const slider = document.getElementById('productSlider');
        if(!slider) return;
        const thumbs = document.querySelectorAll('.slider-thumb');
        const index = Math.round(Math.abs(slider.scrollLeft) / (slider.clientWidth || 1));
        thumbs.forEach((t, i) => t.classList.toggle('active', i === index));
    }
    function goToSlide(index) {
        const slider = document.getElementById('productSlider');
        if(!slider) return;
        const isRTL = document.documentElement.getAttribute('dir') === 'rtl';
        slider.scrollTo({ left: isRTL ? -(index * slider.clientWidth) : (index * slider.clientWidth), behavior: 'smooth' });
    }

    // ================= 6. منطق السعر =================
    let currentQty = 1, basePrice = 990;

    function selectSize(element, priceNum) {
        if(!element) return;
        [...element.parentElement.children].forEach(b => b.classList.remove('active'));
        element.classList.add('active');
        basePrice = priceNum;
        const el = document.getElementById('mainPriceDisplay');
        if(el) el.innerText = basePrice.toLocaleString('en-US');
        // السعر قبل الخصم لهذا الحجم (كل حجم له خصمه) — يظهر مشطوبًا مع نسبة التوفير
        syncOldPriceDisplay(element);
        updateTotalDisplay();
    }
    // يحدّث شطب السعر ونسبة الخصم حسب زر الحجم النشط
    function syncOldPriceDisplay(sizeBtn) {
        const oldEl = document.getElementById('pdOldPrice');
        const pill  = document.getElementById('pdSavePill');
        if (!oldEl || !pill) return;
        const btn = sizeBtn || document.querySelector('#pdSizeGrid .size-btn.active');
        const op = btn ? Number(btn.getAttribute('data-oldprice')) : NaN;
        const cp = btn ? Number(btn.getAttribute('data-price')) : NaN;
        if (isFinite(op) && isFinite(cp) && op > cp && cp > 0) {
            const isRtlNow = document.documentElement.getAttribute('dir') === 'rtl';
            const pct = Math.round((1 - cp / op) * 100);
            oldEl.textContent = op.toLocaleString('en-US');
            oldEl.hidden = false;
            pill.textContent = isRtlNow ? ('وفّرت ' + pct + '%') : ('SAVE ' + pct + '%');
            pill.hidden = false;
        } else {
            oldEl.hidden = true; pill.hidden = true;
        }
    }
    window.syncOldPriceDisplay = syncOldPriceDisplay;
    function changeQty(delta) {
        currentQty = Math.max(1, currentQty + delta);
        const el = document.getElementById('qtyValue');
        if(el) el.innerText = currentQty;
        updateTotalDisplay();
    }
    function updateTotalDisplay() {
        const isEn = document.documentElement.getAttribute('dir') === 'ltr';
        const currency = isEn ? 'EGP' : 'ج.م';
        const total = (basePrice * currentQty);
        const fmt = total.toLocaleString('en-US');
        const bp = document.getElementById('btnPriceDisplay');
        if(bp) bp.innerText = `${fmt} ${currency}`;
        // Sync sticky bar price (base price only, qty handled by stickyQtyVal)
        const sp = document.getElementById('stickyPriceDisplay');
        if(sp) {
            const sQty = parseInt(document.getElementById('stickyQtyVal')?.innerText || 1);
            sp.innerText = (basePrice * sQty).toLocaleString('en-US');
        }
    }

    let stickyQty = 1;
    function changeStickyQty(delta) {
        stickyQty = Math.max(1, stickyQty + delta);
        const el = document.getElementById('stickyQtyVal');
        if(el) el.innerText = stickyQty;
        const sp = document.getElementById('stickyPriceDisplay');
        if(sp) sp.innerText = (basePrice * stickyQty).toLocaleString('en-US');
    }
    function stickyAddToCart() {
        const btn = document.getElementById('stickyAddBtn');
        // ملاحظة: النسخة المعتمدة هي window.stickyAddToCart الأحدث (تقرأ المنتج والحجم الفعليين).
        // هذه نسخة قديمة محتفَظ بها كاحتياط — بدون أي أسماء/صور منتجات ثابتة.
        const name = document.querySelector('#product-detail h1')?.innerText || '';
        if (!name) return;
        addProductToCart({ id: Date.now(), name, nameEn: name, price: basePrice, qty: stickyQty, img: (document.getElementById('stickyProductImg') || {}).src || '', volume: '55 ML' });
        if(btn) {
            btn.classList.add('success');
            const orig = btn.innerHTML;
            const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
            btn.innerHTML = `<svg viewBox="0 0 24 24" style="width:14px;height:14px;stroke:currentColor;fill:none;stroke-width:3;"><polyline points="20 6 9 17 4 12"/></svg><span>${isRtl ? 'تمت!' : 'Added!'}</span>`;
            setTimeout(() => { btn.classList.remove('success'); btn.innerHTML = orig; }, 2000);
        }
    }

    // ================= 7. أنيميشن الإضافة للسلة =================
    function addWithBottleAnim(btn) {
        if(btn.classList.contains('animating') || btn.classList.contains('success')) return;
        btn.classList.add('animating');
        setTimeout(() => {
            btn.classList.remove('animating');
            btn.classList.add('success');
            setTimeout(() => btn.classList.remove('success'), 2500);
        }, 900);
    }

    // addToCartCard — alias يُستخدم في بعض الأزرار
    function addToCartCard(event) {
        if(event) { event.stopPropagation(); event.preventDefault(); }
        const btn = event ? event.currentTarget : null;
        if(btn) addWithBottleAnim(btn);
    }

    // ================= 8. Checkout Logic =================
    // ⚠️ `let` هنا معناها إن المتغير محبوس في بلوك الـ <script> ده. كود التتبع
    // في بلوك تاني، فـ `window.checkoutItems` كان بيرجع undefined دايمًا و
    // InitiateCheckout ما كانش بيتبعت أبدًا. الدالة دي بتخلي البلوكات التانية
    // تقرا القيمة الحقيقية مهما اتغيّرت.
    let checkoutItems = [];
    window.getCheckoutItems = function () { return checkoutItems; };
    let shippingFee = 60, appliedPromoRatio = 0, appliedPaymentDiscount = 0;
    // currentStep = 4 دايمًا: مفيش خطوات، والصفحة كلها متاحة من أول لحظة.
    // القيمة سايبينها موجودة لأن كود قديم بيقراها، لكنها ما بتحجبش أي حاجة.
    let selectedPayment = 'cod', currentStep = 4;

    window._maxStepReached = 4;
    /**
     * بقيت shim فاضية. الـ checkout بقى صفحة واحدة، فمفيش "انتقال لخطوة".
     * سايبينها لأن ٥ مواضع تانية في الملف بتناديها؛ حذفها كان هيرمي
     * ReferenceError وسط عملية الشراء — وده أسوأ مكان ممكن يحصل فيه خطأ.
     * كل اللي بتعمله دلوقتي: تخلي زرار التأكيد جاهز وتوصّل العميل للقسم المطلوب.
     */
    function goToStep(stepNumber) {
        const btn = document.getElementById('btnPlaceOrder');
        if (btn) btn.classList.add('ready');
        // لو الكود القديم بينادي goToStep عشان يوجّه العميل لقسم فيه خطأ،
        // بنعمل تمرير ناعم للقسم ده بدل ما نفتحه (هو مفتوح أصلاً).
        const target = document.getElementById('step' + stepNumber);
        if (target) { try { target.scrollIntoView({ behavior: 'smooth', block: 'start' }); } catch (e) {} }
        return;
    }
    function _legacyGoToStep_unused(stepNumber) {
        // Update high-water mark
        if (stepNumber > window._maxStepReached) window._maxStepReached = stepNumber;
        const max = window._maxStepReached;
        // Apply states to all 3 steps:
        //   - step < current: completed (green check + clickable)
        //   - step == current: active (expanded)
        //   - step > current but <= max: completed (collapsed but clickable, since user got past it)
        //   - step > max: pending (not yet visited)
        document.querySelectorAll('.checkout-step').forEach((s, idx) => {
            const num = idx + 1;
            s.classList.remove('active', 'completed', 'pending');
            if (num === stepNumber) s.classList.add('active');
            else if (num < stepNumber || num <= max) s.classList.add('completed');
            else s.classList.add('pending');
        });
        currentStep = stepNumber;
        const btn = document.getElementById('btnPlaceOrder');
        if (btn) {
            // Show "Place order" whenever user has finished all 3 visible steps
            if (max >= 4 || stepNumber === 4) btn.classList.add('ready');
            else btn.classList.remove('ready');
        }
    }
    window.goToStep = goToStep;

    // ⚠️ نسخة مبكّرة من تعديل الكمية — النسخة النهائية (window.updateCartItemQty) تتحمّل لاحقًا
    // في الملف وتستبدلها. لكن لو العميل ضغط قبل تحميلها (شبكة موبايل بطيئة) لازم دي تكون
    // صحيحة كذلك: تحدّث السلة الحقيقية (cart + localStorage) وليس العرض فقط، وإلا التغيير
    // يضيع بعد الريفريش. للمستخدم المسجّل نفوّض للنسخة النهائية لأنها بتزامن مع السيرفر.
    function updateCartItemQty(id, delta) {
        if (window.updateCartItemQty && window.updateCartItemQty !== updateCartItemQty) {
            return window.updateCartItemQty(id, delta);
        }
        const item = checkoutItems.find(i => i.id === id);
        if(!item) return;
        item.qty = Math.max(1, item.qty + delta);
        // نزامن السلة الحقيقية عشان التغيير يفضل بعد الريفريش
        if (typeof cart !== 'undefined' && Array.isArray(cart)) {
            const c = cart.find(i => i.id === id);
            if (c) { c.qty = item.qty; if (typeof saveGuestCart === 'function') saveGuestCart(); }
        }
        const qEl = document.getElementById(`cartItemQty_${id}`);
        const pEl = document.getElementById(`cartItemPrice_${id}`);
        if(qEl) qEl.innerText = item.qty;
        if(pEl) pEl.innerText = (item.price * item.qty).toLocaleString('en-US');
        if (typeof updateCartBadge === 'function') updateCartBadge();
        calculateOrderSummary();
    }

    // ⚠️ نسخة مبكّرة من الحذف — كانت بتشيل المنتج من العرض (checkoutItems) فقط بدون تحديث
    // السلة الحقيقية، فالمنتج كان **يرجع بعد الريفريش**. لو العميل ضغط قبل تحميل النسخة
    // النهائية (window.removeCartItem) لازم دي تحذف صح كذلك، وللمسجّل نفوّض للنسخة النهائية
    // لأنها بتحذف من السيرفر.
    function removeCartItem(id) {
        if (window.removeCartItem && window.removeCartItem !== removeCartItem) {
            return window.removeCartItem(id);
        }
        const el = document.getElementById(`cartItem_${id}`);
        if(el) { el.style.opacity = '0'; el.style.transition = '0.3s'; }
        setTimeout(() => {
            checkoutItems = checkoutItems.filter(i => i.id !== id);
            // الأهم: نشيله من السلة الحقيقية ونحفظ، وإلا يرجع بعد الريفريش
            if (typeof cart !== 'undefined' && Array.isArray(cart)) {
                cart = cart.filter(i => i.id !== id);
                if (typeof saveGuestCart === 'function') saveGuestCart();
            }
            if(el) el.remove();
            if (typeof updateCartBadge === 'function') updateCartBadge();
            if (typeof renderCartDrawer === 'function') renderCartDrawer();
            calculateOrderSummary();
        }, 300);
    }

    function selectPayment(cardEl, type) {
        document.querySelectorAll('.payment-card').forEach(c => c.classList.remove('active'));
        cardEl.classList.add('active');
        selectedPayment = type;
        appliedPaymentDiscount = type === 'insta' ? 0.05 : 0;
        calculateOrderSummary();
        // تغيير طريقة الدفع يصفّر أخطاء الطريقة السابقة (كانت هتفضل ظاهرة بلا معنى)
        if (typeof window.clearPaymentRefErrors === 'function') window.clearPaymentRefErrors();
        // AddPaymentInfo: اختيار طريقة الدفع هو الفعل الحقيقي المقابل للحدث ده.
        // قبل كده كان مربوطًا بالانتقال لخطوة ٤ — يعني كان بيتبعت حتى لو العميل
        // ما لمسش طريقة الدفع أصلاً (كان سايبها على الافتراضي).
        if (typeof window.trackAddPaymentInfo === 'function') window.trackAddPaymentInfo(type);
    }

    /* ================= تحقق مرجع التحويل (إنستا باي / المحفظة) =================
       المطلوب مختلف حسب الطريقة:
       • المحفظة  → رقم موبايل مصري: ١١ رقم يبدأ بـ 010/011/012/015.
       • إنستا باي → إمّا عنوان IPA بالشكل  اسم@بنك  أو رقم موبايل مصري.
       الأرقام العربية (٠١٢...) بتتحوّل تلقائيًا لإنجليزية قبل الفحص عشان ما نرفضش
       إدخال صحيح كتبه العميل بلوحة مفاتيح عربية. */
    const EG_MOBILE_RE = /^01[0125][0-9]{8}$/;
    const IPA_RE = /^[A-Za-z0-9._-]{3,}@[A-Za-z][A-Za-z0-9._-]{1,}$/;
    function _toEnDigits(s) {
        return String(s || '')
            .replace(/[٠-٩]/g, d => String(d.charCodeAt(0) - 0x0660))   // ٠-٩ عربية
            .replace(/[۰-۹]/g, d => String(d.charCodeAt(0) - 0x06F0));  // ۰-۹ فارسية
    }
    function _payFieldMsg(el, errEl, msgAr, msgEn) {
        const ok = !msgAr;
        if (el) { el.classList.toggle('pay-invalid', !ok); el.classList.toggle('pay-valid', ok && !!el.value.trim()); }
        if (errEl) {
            errEl.hidden = ok;
            if (!ok) {
                errEl.classList.add('lang-text');
                errEl.setAttribute('data-ar', msgAr); errEl.setAttribute('data-en', msgEn);
                errEl.textContent = (document.documentElement.getAttribute('dir') === 'rtl') ? msgAr : msgEn;
            } else { errEl.textContent = ''; errEl.removeAttribute('data-ar'); errEl.removeAttribute('data-en'); }
        }
        return ok;
    }
    // showEmpty=false أثناء الكتابة (ما نلومش العميل قبل ما يخلّص)، true عند الإرسال
    window.validatePaymentRef = function (showEmpty) {
        if (selectedPayment === 'cod') { window.clearPaymentRefErrors(); return true; }
        const isInsta = selectedPayment === 'insta';
        const el = document.getElementById(isInsta ? 'instaInput' : 'walletInput');
        const errEl = document.getElementById(isInsta ? 'instaInputError' : 'walletInputError');
        if (!el) return true;
        const raw = _toEnDigits(el.value).trim().replace(/[\s‏‎-]/g, '');
        if (!raw) {
            if (!showEmpty) return _payFieldMsg(el, errEl, '', '');
            return isInsta
                ? _payFieldMsg(el, errEl, 'اكتب عنوان إنستا باي أو رقم الموبايل اللي حوّلت منه', 'Enter the InstaPay address or mobile number you transferred from')
                : _payFieldMsg(el, errEl, 'اكتب رقم المحفظة اللي حوّلت منه', 'Enter the wallet number you transferred from');
        }
        if (isInsta) {
            if (raw.indexOf('@') > -1) {
                return IPA_RE.test(raw)
                    ? _payFieldMsg(el, errEl, '', '')
                    : _payFieldMsg(el, errEl, 'عنوان إنستا باي غير صحيح — الشكل الصحيح: اسم@بنك (مثال: ahmed@instapay)', 'Invalid InstaPay address — correct format: name@bank (e.g. ahmed@instapay)');
            }
            if (/^\d+$/.test(raw)) {
                return EG_MOBILE_RE.test(raw)
                    ? _payFieldMsg(el, errEl, '', '')
                    : _payFieldMsg(el, errEl, 'رقم الموبايل لازم يكون ١١ رقم ويبدأ بـ 010 أو 011 أو 012 أو 015', 'Mobile number must be 11 digits starting with 010, 011, 012 or 015');
            }
            return _payFieldMsg(el, errEl, 'اكتب عنوان إنستا باي (اسم@بنك) أو رقم موبايل مصري صحيح', 'Enter an InstaPay address (name@bank) or a valid Egyptian mobile number');
        }
        // محفظة: رقم موبايل مصري فقط
        if (!/^\d+$/.test(raw)) return _payFieldMsg(el, errEl, 'رقم المحفظة لازم يكون أرقام بس — من غير حروف أو رموز', 'Wallet number must contain digits only — no letters or symbols');
        return EG_MOBILE_RE.test(raw)
            ? _payFieldMsg(el, errEl, '', '')
            : _payFieldMsg(el, errEl, 'رقم المحفظة لازم يكون ١١ رقم ويبدأ بـ 010 أو 011 أو 012 أو 015', 'Wallet number must be 11 digits starting with 010, 011, 012 or 015');
    };
    window.clearPaymentRefErrors = function () {
        ['instaInput', 'walletInput'].forEach(id => {
            const el = document.getElementById(id), errEl = document.getElementById(id + 'Error');
            if (el) el.classList.remove('pay-invalid', 'pay-valid');
            if (errEl) { errEl.hidden = true; errEl.textContent = ''; }
        });
    };
    // تحقق فوري أثناء الكتابة + عند مغادرة الحقل
    document.addEventListener('DOMContentLoaded', function () {
        ['instaInput', 'walletInput'].forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener('input', () => window.validatePaymentRef(false));
            el.addEventListener('blur',  () => window.validatePaymentRef(true));
        });
    });

    function applyPromo() {
        const val = document.getElementById('promoInput').value.trim().toUpperCase();
        const msg = document.getElementById('promoMsg');
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        if(val === 'REMAL10') {
            appliedPromoRatio = 0.10;
            msg.innerText = isRtl ? '✓ تم تطبيق خصم ١٠٪' : '✓ 10% Discount Applied!';
            msg.className = 'promo-msg success';
        } else if(val === '') {
            appliedPromoRatio = 0; msg.innerText = ''; msg.className = 'promo-msg';
        } else {
            appliedPromoRatio = 0;
            msg.innerText = isRtl ? 'كود غير صحيح أو منتهي' : 'Invalid or expired code';
            msg.className = 'promo-msg error';
        }
        calculateOrderSummary();
    }

    // ===== مناطق الشحن: محافظة + مدن (اختيارية) =====
    // المصدر إعداد shipping_rates_json من لوحة التحكم:
    //   { "v":2, "govs":[ { ar, en, price, cities:[ { ar, en, price } ] } ] }
    // والصيغة القديمة المسطّحة { "القاهرة": 60 } لسه مدعومة وبتتحوّل هنا.
    // ملاحظة: السعر النهائي بيتحسب في السيرفر دايمًا (ShippingRates.cs) واللي هنا عرض
    // تقديري بنفس المنطق بالظبط، فمفيش مجال لتلاعب من المتصفح.
    const EG_GOVS_FALLBACK = [
        ['القاهرة','Cairo'], ['الجيزة','Giza'], ['الإسكندرية','Alexandria'], ['القليوبية','Qalyubia'],
        ['الشرقية','Sharqia'], ['الدقهلية','Dakahlia'], ['الغربية','Gharbia'], ['المنوفية','Monufia'],
        ['كفر الشيخ','Kafr El Sheikh'], ['البحيرة','Beheira'], ['دمياط','Damietta'], ['بورسعيد','Port Said'],
        ['الإسماعيلية','Ismailia'], ['السويس','Suez'], ['شمال سيناء','North Sinai'], ['جنوب سيناء','South Sinai'],
        ['مطروح','Matrouh'], ['الفيوم','Faiyum'], ['بني سويف','Beni Suef'], ['المنيا','Minya'],
        ['أسيوط','Asyut'], ['سوهاج','Sohag'], ['قنا','Qena'], ['الأقصر','Luxor'],
        ['أسوان','Aswan'], ['البحر الأحمر','Red Sea'], ['الوادي الجديد','New Valley']
    ];
    function _num(v) { const n = Number(v); return isFinite(n) && n >= 0 ? n : null; }
    // ملاحظة: isRtl()/esc() معرّفين في بلوك <script> تاني، فبنستخدم نسخ محلية هنا
    // (استدعاؤهم من هنا كان بيرمي ReferenceError ويوقف بناء قائمة المحافظات).
    function _zRtl() { return document.documentElement.getAttribute('dir') !== 'ltr'; }
    function _zEsc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, c =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }
    // بيحوّل أي صيغة محفوظة إلى شكل واحد ثابت: [{ ar, en, price, cities:[{ar,en,price}] }]
    function normalizeShipZones(raw) {
        const byName = {};
        if (raw && typeof raw === 'object' && Array.isArray(raw.govs)) {
            const out = raw.govs.filter(g => g && (g.ar || g.en)).map(g => ({
                ar: String(g.ar || g.en || '').trim(),
                en: String(g.en || g.ar || '').trim(),
                price: _num(g.price),
                cities: (Array.isArray(g.cities) ? g.cities : []).filter(x => x && (x.ar || x.en)).map(x => ({
                    ar: String(x.ar || x.en || '').trim(),
                    en: String(x.en || x.ar || '').trim(),
                    price: _num(x.price)
                }))
            }));
            if (out.length) return out;
        }
        // الصيغة القديمة (أو لا شيء) ← قائمة المحافظات المصرية بأسعارها لو موجودة
        if (raw && typeof raw === 'object') Object.keys(raw).forEach(k => { byName[String(k).trim()] = _num(raw[k]); });
        return EG_GOVS_FALLBACK.map(([ar, en]) => ({ ar, en, price: byName[ar] != null ? byName[ar] : null, cities: [] }));
    }
    function shipZones() {
        if (!Array.isArray(window._shipZones) || !window._shipZones.length)
            window._shipZones = normalizeShipZones(window._shippingRatesRaw);
        return window._shipZones;
    }
    function govByName(name) {
        const v = String(name || '').trim();
        if (!v) return null;
        return shipZones().find(g => g.ar === v || g.en === v) || null;
    }
    // المحافظة / المدينة المختارة حاليًا (فاضي لو لسه ماختارش)
    function selectedGovernorate() {
        const el = document.getElementById('shipGovernorate');
        return el ? (el.value || '').trim() : '';
    }
    function selectedCity() {
        const wrap = document.getElementById('shipCityWrap');
        if (wrap && wrap.hidden) return '';
        const el = document.getElementById('shipCity');
        return el ? (el.value || '').trim() : '';
    }
    // السعر التقديري: سعر المدينة لو متحدد، وإلا سعر المحافظة، وإلا الافتراضي
    function shippingFeeForGovernorate(gov) {
        const def = (typeof window._shippingFee === 'number') ? window._shippingFee : 60;
        const g = govByName(gov != null ? gov : selectedGovernorate());
        if (!g) return def;
        const cityName = selectedCity();
        if (cityName && g.cities.length) {
            const city = g.cities.find(x => x.ar === cityName || x.en === cityName);
            if (city && city.price != null) return city.price;
        }
        return g.price != null ? g.price : def;
    }
    // هل فيه أسعار مختلفة من منطقة لتانية؟ (عشان نقول للعميل إن الرقم تقديري)
    function hasZonePricing() {
        return shipZones().some(g => g.price != null || g.cities.some(x => x.price != null));
    }
    // بناء قائمة المحافظات من الإعدادات (القيمة بالعربي دايمًا — هي مفتاح السعر في السيرفر)
    function buildGovernorateOptions() {
        const sel = document.getElementById('shipGovernorate');
        if (!sel) return;
        const keep = sel.value;
        const head = '<option value="" class="lang-text" data-ar="المحافظة" data-en="Governorate">'
            + (_zRtl() ? 'المحافظة' : 'Governorate') + '</option>';
        sel.innerHTML = head + shipZones().map(g =>
            '<option value="' + _zEsc(g.ar) + '" class="lang-text" data-ar="' + _zEsc(g.ar) + '" data-en="' + _zEsc(g.en) + '">'
            + _zEsc(_zRtl() ? g.ar : g.en) + '</option>').join('');
        if (keep) sel.value = keep;
        onGovernorateChange(true);
    }
    // حقل المدينة بيظهر فقط لو المحافظة اتضافلها مدن من لوحة التحكم، ووقتها بيبقى إلزامي
    window.onGovernorateChange = function (keepCity) {
        const wrap = document.getElementById('shipCityWrap');
        const sel = document.getElementById('shipCity');
        const g = govByName(selectedGovernorate());
        const cities = g ? g.cities : [];
        if (sel) {
            const keep = keepCity ? sel.value : '';
            const head = '<option value="" class="lang-text" data-ar="المدينة" data-en="City">'
                + (_zRtl() ? 'المدينة' : 'City') + '</option>';
            sel.innerHTML = head + cities.map(x =>
                '<option value="' + _zEsc(x.ar) + '" class="lang-text" data-ar="' + _zEsc(x.ar) + '" data-en="' + _zEsc(x.en) + '">'
                + _zEsc(_zRtl() ? x.ar : x.en) + '</option>').join('');
            sel.value = (keep && cities.some(x => x.ar === keep)) ? keep : '';
        }
        if (wrap) {
            wrap.hidden = cities.length === 0;
            // مفيش مدن → مفيش رسالة خطأ معلقة من اختيار سابق
            if (wrap.hidden && sel) {
                sel.classList.remove('ship-invalid', 'ship-valid');
                const m = wrap.querySelector('.ship-err-msg'); if (m) m.textContent = '';
            }
        }
        if (typeof calculateOrderSummary === 'function') calculateOrderSummary();
    };
    window.onCityChange = function () {
        const sel = document.getElementById('shipCity');
        if (sel && sel.value) { sel.classList.remove('ship-invalid'); sel.classList.add('ship-valid');
            const m = sel.parentNode && sel.parentNode.querySelector('.ship-err-msg'); if (m) m.textContent = ''; }
        if (typeof calculateOrderSummary === 'function') calculateOrderSummary();
    };
    window.buildGovernorateOptions = buildGovernorateOptions;
    window.selectedCity = selectedCity;
    window.hasZonePricing = hasZonePricing;
    // بناء أولي بقائمة المحافظات الافتراضية لحد ما إعدادات الشحن توصل من الـ API
    // (loadSettings بتعيد البناء بعدها بالمناطق والأسعار المحفوظة).
    (function initGovOptions() {
        const run = function () { try { buildGovernorateOptions(); } catch (e) {} };
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run);
        else run();
    })();

    function calculateOrderSummary() {
        const subtotal = checkoutItems.reduce((s, i) => s + i.price * i.qty, 0);
        // Free shipping based on the admin-configured threshold (falls back to 2000).
        const _thr = (typeof freeShippingThreshold === 'number' && freeShippingThreshold > 0) ? freeShippingThreshold : 2000;
        const _fee = shippingFeeForGovernorate(selectedGovernorate());
        shippingFee = subtotal >= _thr ? 0 : _fee;
        const discountAmt = subtotal * (appliedPromoRatio + appliedPaymentDiscount);
        const total = subtotal + shippingFee - discountAmt;

        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const s = document.getElementById('sumSubtotal');
        if(s) s.innerText = subtotal.toLocaleString('en-US');
        const sh = document.getElementById('sumShipping');
        if (sh) {
            if (shippingFee === 0) sh.innerText = isRtl ? 'مجاني' : 'FREE';
            else if (!selectedGovernorate() && hasZonePricing())
                // فيه أسعار مختلفة حسب المحافظة ولسه ماختارهاش → نوضح إنه تقديري
                sh.innerText = isRtl ? `${shippingFee} ج.م (يُحدَّد بالمحافظة)` : `${shippingFee} EGP (by governorate)`;
            else sh.innerText = `${shippingFee} EGP`;
        }
        const dr = document.getElementById('sumDiscountRow');
        if(dr) dr.style.display = discountAmt > 0 ? 'flex' : 'none';
        const dv = document.getElementById('sumDiscountVal');
        if(dv) dv.innerText = Math.round(discountAmt).toLocaleString('en-US');
        const ft = document.getElementById('sumFinalTotal');
        if(ft) ft.innerText = Math.round(total).toLocaleString('en-US');
    }

    function placeOrder() {
        if(currentStep < 4) {
            const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
            alert(isRtl ? 'برجاء إكمال كل الخطوات أولاً' : 'Please complete all steps first');
            return;
        }
        // نفس تحقق مرجع التحويل المستخدم في النسخة النهائية (رسالة تحت الحقل، بدون alert)
        if (window.validatePaymentRef && !window.validatePaymentRef(true)) {
            const el = document.getElementById(selectedPayment === 'insta' ? 'instaInput' : 'walletInput');
            if (el) { try { el.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch (e) {} el.focus(); }
            return;
        }
        const btn = document.getElementById('btnPlaceOrder');
        btn.style.opacity = '0.6'; btn.style.pointerEvents = 'none';
        btn.innerHTML = `<span style="font-family:'Montserrat',sans-serif;letter-spacing:2px;">...</span>`;
        setTimeout(() => {
            const orderId = `RML-${Math.floor(100000 + Math.random() * 900000)}`;
            const el = document.getElementById('displayOrderId');
            if(el) el.innerText = orderId;
            cart = []; updateCartBadge(); renderCartDrawer();
            btn.style.opacity = '1'; btn.style.pointerEvents = 'auto';
            const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
            btn.innerHTML = `<span>${isRtl ? 'تأكيد الطلب' : 'PLACE ORDER'}</span>`;
            navigate('order-success');
        }, 1500);
    }

    document.addEventListener("DOMContentLoaded", () => { calculateOrderSummary(); });

    // ================= 9. مزودي المحفظة =================
    function selectWalletProvider(event, btn, provider) {
        event.stopPropagation();
        document.querySelectorAll('.wallet-provider-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const notes = {
            vodafone: { ar: 'حول من فودافون كاش وابعت لقطة الشاشة على واتساب', en: 'Transfer via Vodafone Cash and send the screenshot on WhatsApp' },
            orange:   { ar: 'حول من أورانج كاش وابعت لقطة الشاشة على واتساب',  en: 'Transfer via Orange Cash and send the screenshot on WhatsApp' },
            etisalat: { ar: 'حول من اتصالات كاش وابعت لقطة الشاشة على واتساب', en: 'Transfer via Etisalat Cash and send the screenshot on WhatsApp' }
        };
        const note = document.getElementById('walletProviderNote');
        if(note && notes[provider]) note.innerText = isRtl ? notes[provider].ar : notes[provider].en;
    }

    // ================= 10. نسخ الرقم =================
    function copyNumber(event, number) {
        event.stopPropagation();
        const btn = event.currentTarget;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        navigator.clipboard.writeText(number).then(() => {
            btn.classList.add('copied');
            const orig = btn.innerHTML;
            btn.innerHTML = `<svg viewBox="0 0 24 24" style="width:14px;height:14px;stroke:currentColor;fill:none;stroke-width:2.5;stroke-linecap:round;"><polyline points="20 6 9 17 4 12"/></svg><span>${isRtl ? 'تم!' : 'Copied!'}</span>`;
            setTimeout(() => { btn.classList.remove('copied'); btn.innerHTML = orig; }, 2000);
        }).catch(() => {
            const ta = Object.assign(document.createElement('textarea'), { value: number });
            document.body.appendChild(ta); ta.select(); document.execCommand('copy'); ta.remove();
        });
    }

    // ================= 11. Order Tracking =================
    function trackOrderNow() {
        const input = document.getElementById('trackInput');
        const btn = document.getElementById('btnTrackSubmit');
        const resultBox = document.getElementById('trackingResultBox');
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        if(!input.value.trim()) { alert(isRtl ? 'برجاء إدخال رقم الطلب' : 'Please enter your order number'); return; }
        btn.classList.add('loading');
        resultBox.classList.remove('show');
        setTimeout(() => {
            btn.classList.remove('loading');
            document.getElementById('resOrderId').innerText = input.value.trim().toUpperCase();
            resultBox.classList.add('show');
            resultBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }, 1200);
    }

    // ================= 12. Catalog Filter =================
    function filterCatalog(type, btn) {
        document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const items = document.querySelectorAll('#catalogGrid .catalog-item');
        let sorted = [...items];
        if(type === 'all') { sorted.forEach(i => i.style.display = ''); return; }
        if(type === 'bestseller') { items.forEach(i => { i.style.display = i.dataset.best === 'true' ? '' : 'none'; }); return; }
        if(type === 'new') { items.forEach(i => { i.style.display = i.dataset.new === 'true' ? '' : 'none'; }); return; }
        if(type === 'men') { items.forEach(i => { i.style.display = (i.dataset.cat || '').includes('men') ? '' : 'none'; }); return; }
        if(type === 'unisex') { items.forEach(i => { i.style.display = (i.dataset.cat || '').includes('unisex') ? '' : 'none'; }); return; }
        if(type === 'price-low' || type === 'price-high') {
            const grid = document.getElementById('catalogGrid');
            sorted.sort((a, b) => {
                const pa = parseInt(a.dataset.price || 0), pb = parseInt(b.dataset.price || 0);
                return type === 'price-low' ? pa - pb : pb - pa;
            });
            sorted.forEach(i => { i.style.display = ''; grid.appendChild(i); });
        }
    }


    // ================= WISHLIST SYSTEM =================
    let wishlist = JSON.parse(sessionStorage.getItem('remal_wishlist') || '[]');
    let isLoggedIn = sessionStorage.getItem('remal_loggedIn') === 'true';

    function saveWishlist() {
        sessionStorage.setItem('remal_wishlist', JSON.stringify(wishlist));
    }

    function getWishlistCount() {
        return wishlist.length;
    }

    function updateWishlistBadge() {
        const badge = document.getElementById('wishlistBadge');
        if(!badge) return;
        const count = getWishlistCount();
        badge.innerText = count;
        badge.classList.toggle('show', count > 0);
    }

    function toggleWishlistDrawer() {
        const drawer = document.getElementById('wishlistDrawer');
        const overlay = document.getElementById('wishlistOverlay');
        if(!drawer || !overlay) return;
        const isOpen = drawer.classList.contains('open');
        if(isOpen) {
            drawer.classList.remove('open');
            overlay.classList.remove('open');
            document.body.style.overflow = '';
        } else {
            renderWishlistDrawer();
            drawer.classList.add('open');
            overlay.classList.add('open');
            document.body.style.overflow = 'hidden';
            // Close other drawers
            closeCartDrawer();
            const mnav = document.getElementById('mobileNav');
            const moverlay = document.getElementById('mobileNavOverlay');
            if(mnav) mnav.classList.remove('open');
            if(moverlay) moverlay.classList.remove('open');
        }
    }

    function renderWishlistDrawer() {
        const body = document.getElementById('wishlistDrawerBody');
        const countEl = document.getElementById('wishlistDrawerCount');
        if(!body) return;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        if(countEl) countEl.innerText = getWishlistCount();

        if(wishlist.length === 0) {
            body.innerHTML = `<div class="wishlist-empty">
                <svg viewBox="0 0 24 24"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>
                <h3 class="lang-text" data-ar="قائمة أمنياتك فارغة" data-en="Your wishlist is empty">قائمة أمنياتك فارغة</h3>
                <p class="lang-text" data-ar="اضغط على القلب في أي عطر يعجبك" data-en="Tap the heart on any fragrance you love">اضغط على القلب في أي عطر يعجبك</p>
            </div>`;
            return;
        }

        body.innerHTML = wishlist.map((item, idx) => `
            <div class="wishlist-item">
                <img loading="lazy" decoding="async" src="${item.img}" alt="${item.name}">
                <div class="wishlist-item-info">
                    <div class="wishlist-item-name">${isRtl ? item.name : (item.nameEn || item.name)}</div>
                    <div class="wishlist-item-price en-num">${item.price.toLocaleString('en-US')} ${isRtl ? 'ج.م' : 'EGP'}</div>
                    <div class="wishlist-item-actions">
                        <button class="wishlist-add-btn" onclick="addFromWishlist(${idx})">
                            ${isRtl ? 'أضِف إلى الحقيبة' : 'ADD TO BAG'}
                        </button>
                        <button class="wishlist-remove-btn" onclick="removeFromWishlist(${idx})">
                            <svg viewBox="0 0 24 24"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/></svg>
                        </button>
                    </div>
                </div>
            </div>
        `).join('');
    }

    function addFromWishlist(idx) {
        const item = wishlist[idx];
        if(!item) return;
        addProductToCart({ id: Date.now(), name: item.name, nameEn: item.nameEn, price: item.price, qty: 1, img: item.img, volume: item.volume || '55 ML' });
    }

    function removeFromWishlist(idx) {
        wishlist.splice(idx, 1);
        saveWishlist();
        updateWishlistBadge();
        renderWishlistDrawer();
        renderAccountWishlist();
        // Sync heart icons
        syncHeartIcons();
    }

    function syncHeartIcons() {
        document.querySelectorAll('.heart-icon').forEach(h => {
            const card = h.closest('.noon-card');
            if(!card) return;
            const nameEl = card.querySelector('.product-title');
            if(!nameEl) return;
            const name = nameEl.innerText.trim();
            const inWishlist = wishlist.some(w => w.name === name);
            h.classList.toggle('liked', inWishlist);
            h.innerHTML = inWishlist ? '❤' : '♡';
        });
    }

    function renderAccountWishlist() {
        const container = document.getElementById('accountWishlistBody');
        if(!container) return;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        if(wishlist.length === 0) {
            container.innerHTML = `<div class="wishlist-empty">
                <svg viewBox="0 0 24 24"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/></svg>
                <h3 class="lang-text" data-ar="قائمة أمنياتك فارغة" data-en="Your wishlist is empty">قائمة أمنياتك فارغة</h3>
                <p class="lang-text" data-ar="اضغط على القلب في أي عطر يعجبك" data-en="Tap the heart on any fragrance you love">اضغط على القلب في أي عطر يعجبك</p>
            </div>`;
            return;
        }
        container.innerHTML = `<div class="grid" style="grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));">` + wishlist.map((item, idx) => `
            <div class="noon-card" style="cursor: default;">
                <div class="image-area" style="aspect-ratio: 1/1;">
                    <img loading="lazy" decoding="async" src="${item.img}" class="product-img" alt="${item.name}">
                </div>
                <div class="info-area">
                    <h3 class="product-title">${item.name}</h3>
                    <div class="price-volume-row">
                        <div class="price-area"><span class="currency">${isRtl ? 'ج.م' : 'EGP'}</span><span class="amount en-num">${item.price.toLocaleString('en-US')}</span></div>
                    </div>
                    <div style="display: flex; gap: 8px;">
                        <button class="wishlist-add-btn" style="flex: 1;" onclick="addFromWishlist(${idx})">${isRtl ? 'أضِف إلى الحقيبة' : 'ADD TO BAG'}</button>
                        <button class="wishlist-remove-btn" onclick="removeFromWishlist(${idx})">
                            <svg viewBox="0 0 24 24" style="width: 14px; height: 14px; stroke: currentColor; fill: none; stroke-width: 2;"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/></svg>
                        </button>
                    </div>
                </div>
            </div>
        `).join('') + `</div>`;
    }

    // Wire up heart icons
    function initHeartIcons() {
        document.querySelectorAll('.heart-icon').forEach(h => {
            h.addEventListener('click', function(e) {
                e.stopPropagation();
                e.preventDefault();
                const card = this.closest('.noon-card');
                if(!card) return;
                const nameEl = card.querySelector('.product-title');
                const priceEl = card.querySelector('.amount');
                const imgEl = card.querySelector('.product-img');
                if(!nameEl || !priceEl) return;
                const name = nameEl.innerText.trim();
                const price = parseInt(priceEl.innerText.replace(/,/g, '')) || 990;
                const img = imgEl ? imgEl.src : '';
                const nameEn = name;
                const existingIdx = wishlist.findIndex(w => w.name === name);
                if(existingIdx >= 0) {
                    wishlist.splice(existingIdx, 1);
                    this.classList.remove('liked');
                    this.innerHTML = '♡';
                } else {
                    wishlist.push({ name, nameEn, price, img, volume: '55 ML' });
                    this.classList.add('liked');
                    this.innerHTML = '❤';
                }
                saveWishlist();
                updateWishlistBadge();
            });
        });
    }

    // ================= AUTHENTICATION =================
    function handleAuthClick() {
        if(isLoggedIn) { navigate('account'); }
        else { navigate('login'); }
    }

    function handleLogin() {
        // Any input is accepted
        isLoggedIn = true;
        sessionStorage.setItem('remal_loggedIn', 'true');
        updateAuthUI();
        navigate('account');
    }

    function handleRegister() {
        const pass = document.getElementById('regPassword')?.value;
        const confirm = document.getElementById('regConfirmPass')?.value;
        if(pass !== confirm) {
            const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
            alert(isRtl ? 'الباسورد مش متطابق' : 'Passwords do not match');
            return;
        }
        isLoggedIn = true;
        sessionStorage.setItem('remal_loggedIn', 'true');
        updateAuthUI();
        navigate('account');
    }

    function handleLogout() {
        isLoggedIn = false;
        sessionStorage.removeItem('remal_loggedIn');
        updateAuthUI();
        navigate('home');
    }

    function updateAuthUI() {
        const authBtn = document.getElementById('navAuthBtn');
        const authText = document.getElementById('authBtnText');
        const authIcon = document.getElementById('authBtnIcon');
        if(!authBtn || !authText) return;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        if(isLoggedIn) {
            authText.innerText = isRtl ? 'حسابي' : 'ACCOUNT';
            if(authIcon) authIcon.innerHTML = '<circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>';
        } else {
            authText.innerText = isRtl ? 'دخول' : 'SIGN IN';
            if(authIcon) authIcon.innerHTML = '<circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/>';
        }
        // Update mobile nav footer
        updateMobileNavAuth();
    }

    function updateMobileNavAuth() {
        const footer = document.querySelector('.mnav-footer');
        if(!footer) return;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        if(isLoggedIn) {
            footer.innerHTML = `
                <button class="mnav-account-btn" onclick="navigate('account')">
                    <svg viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg>
                    <span class="lang-text" data-ar="حسابي" data-en="MY ACCOUNT">حسابي</span>
                </button>
                <button class="mnav-logout-btn" onclick="handleLogout()">
                    <svg viewBox="0 0 24 24"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
                    <span class="lang-text" data-ar="تسجيل الخروج" data-en="LOGOUT">تسجيل الخروج</span>
                </button>`;
        } else {
            footer.innerHTML = `
                <button class="mnav-auth-btn mnav-login" onclick="navigate('login')">
                    <svg viewBox="0 0 24 24"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg>
                    <span class="lang-text" data-ar="تسجيل الدخول" data-en="SIGN IN">تسجيل الدخول</span>
                </button>
                <button class="mnav-auth-btn mnav-register" onclick="navigate('register')">
                    <svg viewBox="0 0 24 24"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><line x1="19" y1="8" x2="19" y2="14"/><line x1="22" y1="11" x2="16" y2="11"/></svg>
                    <span class="lang-text" data-ar="إنشاء حساب" data-en="REGISTER">إنشاء حساب</span>
                </button>`;
        }
        // Re-apply language
        const html = document.documentElement;
        const newLang = html.getAttribute('dir') === 'rtl' ? 'ar' : 'en';
        footer.querySelectorAll('.lang-text').forEach(el => {
            const t = el.getAttribute(`data-${newLang}`);
            if(t) el.innerText = t;
        });
    }

    // ================= ACCOUNT TABS =================
    function switchAccountTab(tabName) {
        document.querySelectorAll('.account-sidebar-tab').forEach(t => t.classList.toggle('active', t.dataset.tab === tabName));
        document.querySelectorAll('.account-mobile-tab').forEach(t => t.classList.toggle('active', t.dataset.tab === tabName));
        document.querySelectorAll('.account-tab-panel').forEach(p => p.classList.toggle('active', p.id === 'tab-' + tabName));
        if(tabName === 'wishlistTab') renderAccountWishlist();
        // افتكر آخر تبويب — بعد الريفريش يرجع العميل لنفس المكان
        try { sessionStorage.setItem('remal_account_tab', tabName); } catch(e) {}
    }

    function saveProfile() {
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        alert(isRtl ? 'تم حفظ التغييرات!' : 'Changes saved!');
    }

    function copyCoupon(btn, code) {
        navigator.clipboard.writeText(code).then(() => {
            const orig = btn.innerHTML;
            btn.style.background = 'var(--green)';
            setTimeout(() => { btn.style.background = ''; btn.innerHTML = orig; }, 1500);
        }).catch(() => {
            const ta = Object.assign(document.createElement('textarea'), { value: code });
            document.body.appendChild(ta); ta.select(); document.execCommand('copy'); ta.remove();
        });
    }

    // ================= ENHANCED NAVIGATE (sand-particle transition removed) =================
    const originalNavigate = navigate;
    navigate = function(pageId) {
        originalNavigate(pageId);
        // Sync checkout cart whenever the checkout page opens
        if(pageId === 'checkout' && typeof syncCheckoutFromCart === 'function') {
            try { syncCheckoutFromCart(); } catch(e) {}
        }
        // Section entrance animation
        const activeSection = document.querySelector('.page-section.active');
        if(activeSection) {
            const children = activeSection.querySelectorAll('.entrance-anim, .auth-form-box, .order-card, .coupon-card, .account-tab-panel.active > *, .points-card, .profile-form, .wishlist-empty, .orders-empty, .coupons-empty');
            children.forEach((el, i) => {
                el.style.opacity = '0';
                el.style.transform = 'translateY(20px)';
                el.style.transition = 'opacity 0.5s ease, transform 0.5s ease';
                setTimeout(() => {
                    el.style.opacity = '1';
                    el.style.transform = 'translateY(0)';
                }, i * 80);
            });
        }
        // 404 sand animation (kept — only the page-transition sand was removed)
        if(pageId === 'notfound') { init404Animation(); }
        // Update auth UI
        updateAuthUI();
    };

    // ================= 404 ANIMATION =================
    let countdownInterval;
    function init404Animation() {
        const grainsContainer = document.getElementById('sandGrains');
        if(!grainsContainer) return;
        grainsContainer.innerHTML = '';
        for(let i = 0; i < 40; i++) {
            const g = document.createElement('div');
            g.className = 'sand-grain-404';
            g.style.left = (10 + Math.random() * 80) + '%';
            g.style.top = (10 + Math.random() * 80) + '%';
            g.style.animationDelay = (Math.random() * 2) + 's';
            g.style.animationDuration = (1 + Math.random()) + 's';
            grainsContainer.appendChild(g);
        }
        // Countdown
        let count = 10;
        const numEl = document.getElementById('countNum');
        if(numEl) numEl.innerText = count;
        if(countdownInterval) clearInterval(countdownInterval);
        countdownInterval = setInterval(() => {
            count--;
            if(numEl) numEl.innerText = count;
            if(count <= 0) { clearInterval(countdownInterval); navigate('home'); }
        }, 1000);
    }

    // ================= MOBILE LANG TOGGLE =================
    function setLanguageFromMobile(lang) {
        const html = document.documentElement;
        const currentLang = html.getAttribute('dir') === 'rtl' ? 'ar' : 'en';
        if(lang !== currentLang) toggleLanguage();
        syncMnavLangButtons();
    }
    function syncMnavLangButtons() {
        const lang = document.documentElement.getAttribute('dir') === 'rtl' ? 'ar' : 'en';
        const ar = document.getElementById('mnavLangAr');
        const en = document.getElementById('mnavLangEn');
        if(ar) ar.classList.toggle('active', lang === 'ar');
        if(en) en.classList.toggle('active', lang === 'en');
    }

    // ================= BUNDLES =================
    const BUNDLES = {
        summer: {
            ar: { title: 'باقة الصيف', lead: '٣ روائح منعشة لأيام الصيف الحارة، اختارهم خبراؤنا عشانك. حمضيات، شاي أسود، ونسمة بحر — كل اللي محتاجه عشان تفوح بانتعاش طول اليوم.' },
            en: { title: 'The Summer Bundle', lead: 'Three refreshing scents for hot summer days, hand-picked by our experts.' },
            cover: 'https://remal-perfume.runasp.net/freshSummer.webp',
            original: 2970, savings: 570, final: 2400,
            items: [
                { ar: 'فريش سَمر - Fresh Summer', en: 'Fresh Summer', dsAr: 'حمضيات وشاي أسود', dsEn: 'Citrus & black tea', img: 'https://remal-perfume.runasp.net/freshSummer.webp', vol: '55 ML' },
                { ar: 'سيتروس فلير - Citrus Flare', en: 'Citrus Flare', dsAr: 'جريب فروت وأمبروكسان', dsEn: 'Grapefruit & ambroxan', img: 'https://remal-perfume.runasp.net/citrusFlare.webp', vol: '55 ML' },
                { ar: 'تروبيكال موس', en: 'Tropical Moss', dsAr: 'موس أخضر ونعناع', dsEn: 'Green moss & mint', img: 'https://remal-perfume.runasp.net/roastedMocha.webp', vol: '55 ML' }
            ]
        },
        winter: {
            ar: { title: 'باقة الشتا', lead: 'دفا الكراميل والعود لسهرات الشتا الدافية. الباقة المثالية لمن يحب الروائح الدافئة والقوية.' },
            en: { title: 'The Winter Bundle', lead: 'Warm caramel and oud for cozy winter nights.' },
            cover: 'https://remal-perfume.runasp.net/amberAddiction.webp',
            original: 3090, savings: 590, final: 2500,
            items: [
                { ar: 'أمبر أديکشن - Amber Addiction', en: 'Amber Addiction', dsAr: 'كراميل وفانيليا', dsEn: 'Caramel & vanilla', img: 'https://remal-perfume.runasp.net/amberAddiction.webp', vol: '55 ML' },
                { ar: 'ميستيك عود - Mystic Oud', en: 'Mystic Oud', dsAr: 'عود وبخور', dsEn: 'Oud & incense', img: 'https://remal-perfume.runasp.net/mysticOud.webp', vol: '55 ML' },
                { ar: 'روستد موكا', en: 'Roasted Mocha', dsAr: 'قهوة وفانيليا', dsEn: 'Coffee & vanilla', img: 'https://remal-perfume.runasp.net/roastedMocha.webp', vol: '55 ML' }
            ]
        },
        signature: {
            ar: { title: 'باقة السيجنتشر', lead: 'أكثر ٣ روائح طلباً عند رمال — لو حابب تجرب الأشهر.' },
            en: { title: 'The Signature Bundle', lead: 'The 3 most-requested Remal scents.' },
            cover: 'https://remal-perfume.runasp.net/mysticOud.webp',
            original: 2970, savings: 370, final: 2600,
            items: [
                { ar: 'فريش سَمر - Fresh Summer', en: 'Fresh Summer', dsAr: 'حمضيات وشاي أسود', dsEn: 'Citrus & black tea', img: 'https://remal-perfume.runasp.net/freshSummer.webp', vol: '55 ML' },
                { ar: 'ميستيك عود - Mystic Oud', en: 'Mystic Oud', dsAr: 'عود وبخور', dsEn: 'Oud & incense', img: 'https://remal-perfume.runasp.net/mysticOud.webp', vol: '55 ML' },
                { ar: 'ليكويد جولد - Liquid Gold', en: 'Liquid Gold', dsAr: 'عسل وتبغ', dsEn: 'Honey & tobacco', img: 'https://remal-perfume.runasp.net/liquidGold.webp', vol: '55 ML' }
            ]
        },
        gift: {
            ar: { title: 'باقة الهدية الفاخرة', lead: 'عطرين فاخرين مع تغليف خشبي وكارت يدوي مكتوب عشان تكون هدية لا تنسى.' },
            en: { title: 'The Luxury Gift Bundle', lead: 'Two premium scents with wooden gift wrap and a handwritten card.' },
            cover: 'https://remal-perfume.runasp.net/liquidGold.webp',
            original: 2140, savings: 340, final: 1800,
            items: [
                { ar: 'ليكويد جولد - Liquid Gold', en: 'Liquid Gold', dsAr: 'عسل وتبغ', dsEn: 'Honey & tobacco', img: 'https://remal-perfume.runasp.net/liquidGold.webp', vol: '55 ML' },
                { ar: 'أمبر أديکشن - Amber Addiction', en: 'Amber Addiction', dsAr: 'كراميل وفانيليا', dsEn: 'Caramel & vanilla', img: 'https://remal-perfume.runasp.net/amberAddiction.webp', vol: '55 ML' }
            ]
        }
    };
    let currentBundleKey = 'summer';

    function openBundle(key) {
        if(!BUNDLES[key]) return;
        currentBundleKey = key;
        const b = BUNDLES[key];
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const lang = isRtl ? 'ar' : 'en';
        // Title + lead
        const titleEl = document.getElementById('bdTitle');
        const leadEl = document.getElementById('bdLead');
        if(titleEl) {
            titleEl.setAttribute('data-ar', b.ar.title);
            titleEl.setAttribute('data-en', b.en.title);
            titleEl.textContent = b[lang].title;
        }
        if(leadEl) {
            const subAr = b.items.length + ' روائح منتقاة بعناية — ' + b.ar.title;
            const subEn = b.items.length + ' scents curated with care — ' + b.en.title;
            leadEl.setAttribute('data-ar', subAr);
            leadEl.setAttribute('data-en', subEn);
            leadEl.textContent = isRtl ? subAr : subEn;
        }
        // 3-image slider — use bundle items' images
        const imgs = b.items.slice(0, 3).map(i => i.img);
        while(imgs.length < 3) imgs.push(b.cover);
        ['bdSlide1', 'bdSlide2', 'bdSlide3'].forEach((id, i) => { const el = document.getElementById(id); if(el) el.src = imgs[i]; });
        ['bdThumb1', 'bdThumb2', 'bdThumb3'].forEach((id, i) => { const el = document.getElementById(id); if(el) el.src = imgs[i]; });
        // Breadcrumb
        const bc = document.getElementById('bdBreadcrumb');
        if(bc && bc.parentElement) {
            const arBC = 'الرئيسية / الباقات / ' + b.ar.title;
            const enBC = 'Home / Bundles / ' + b.en.title;
            bc.parentElement.setAttribute('data-ar', arBC);
            bc.parentElement.setAttribute('data-en', enBC);
            bc.textContent = isRtl ? arBC : enBC;
        }
        // Items list
        const list = document.getElementById('bdItemsList');
        if(list) {
            list.innerHTML = b.items.map(it => `
                <div class="bd-include-row" onclick="navigate('product-detail')" style="cursor:pointer;">
                    <img loading="lazy" decoding="async" src="${it.img}" alt="${it.en}">
                    <div class="info" style="flex:1;min-width:0;">
                        <div class="nm lang-text" data-ar="${it.ar}" data-en="${it.en}">${isRtl ? it.ar : it.en}</div>
                        <div class="ds lang-text" data-ar="${it.dsAr}" data-en="${it.dsEn}">${isRtl ? it.dsAr : it.dsEn}</div>
                    </div>
                    <span class="vol en-num">${it.vol}</span>
                </div>`).join('');
        }
        // Pricing
        const o = document.getElementById('bdOriginal'); if(o) o.innerText = b.original.toLocaleString('en-US');
        const s = document.getElementById('bdSavings'); if(s) s.innerText = b.savings.toLocaleString('en-US');
        const f = document.getElementById('bdFinal'); if(f) f.innerText = b.final.toLocaleString('en-US');
        const op = document.getElementById('bdOriginalPrice'); if(op) op.innerText = b.original.toLocaleString('en-US');
        const pd = document.getElementById('bdPriceDisplay'); if(pd) pd.innerText = b.final.toLocaleString('en-US');
        const sl = document.getElementById('bdSavingsLabel');
        if(sl) {
            const arS = 'وفر ' + b.savings.toLocaleString('en-US') + ' ج.م';
            const enS = 'Save ' + b.savings.toLocaleString('en-US') + ' EGP';
            sl.setAttribute('data-ar', arS);
            sl.setAttribute('data-en', enS);
            sl.textContent = isRtl ? arS : enS;
        }
        bundleQty = 1;
        const qv = document.getElementById('bdQtyValue'); if(qv) qv.innerText = 1;
        updateBdBtnPrice();
        navigate('bundle-detail');
    }

    // Collection-detail qty + price
    let collectionQty = 1;
    function changeCdQty(delta) {
        collectionQty = Math.max(1, collectionQty + delta);
        const v = document.getElementById('cdQtyValue'); if(v) v.innerText = collectionQty;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const cur = isRtl ? 'ج.م' : 'EGP';
        const total = 250 * collectionQty;
        const bd = document.getElementById('cdBtnPriceDisplay');
        if(bd) bd.innerText = total.toLocaleString('en-US') + ' ' + cur;
    }
    function buyNowCollection() {
        addProductToCart({ id: Date.now(), name: 'مجموعة الاكتشاف الشاملة', nameEn: 'The Ultimate Discovery Set', price: 250, qty: collectionQty, img: 'https://remal-perfume.runasp.net/disk1.webp', volume: '6 × 5 ML' });
        navigate('checkout');
    }
    function addCollectionToCartFromCard(event) {
        if(event) { event.stopPropagation(); event.preventDefault(); }
        const btn = event ? event.currentTarget : null;
        addProductToCart({ id: Date.now(), name: 'مجموعة الاكتشاف الشاملة', nameEn: 'The Ultimate Discovery Set', price: 250, qty: 1, img: 'https://remal-perfume.runasp.net/disk1.webp', volume: '6 × 5 ML' });
        if(btn) addWithBottleAnim(btn);
    }

    // Bundle-detail qty + price
    let bundleQty = 1;
    function changeBdQty(delta) {
        bundleQty = Math.max(1, bundleQty + delta);
        const v = document.getElementById('bdQtyValue'); if(v) v.innerText = bundleQty;
        updateBdBtnPrice();
    }
    function updateBdBtnPrice() {
        const b = BUNDLES[currentBundleKey];
        if(!b) return;
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        const cur = isRtl ? 'ج.م' : 'EGP';
        const total = b.final * bundleQty;
        const bd = document.getElementById('bdBtnPriceDisplay');
        if(bd) bd.innerText = total.toLocaleString('en-US') + ' ' + cur;
    }
    function buyNowBundle() {
        const b = BUNDLES[currentBundleKey];
        if(!b) return;
        addProductToCart({ id: Date.now(), name: b.ar.title + ' (Bundle)', nameEn: b.en.title + ' (Bundle)', price: b.final, qty: bundleQty, img: b.cover, volume: b.items.length + 'x ' + b.items[0].vol });
        navigate('checkout');
    }

    // Sliders for collection-detail and bundle-detail
    function updateCdSliderDots() {
        const slider = document.getElementById('cdSlider');
        if(!slider) return;
        const thumbs = slider.parentElement.querySelectorAll('.slider-thumb');
        const idx = Math.round(Math.abs(slider.scrollLeft) / (slider.clientWidth || 1));
        thumbs.forEach((t, i) => t.classList.toggle('active', i === idx));
    }
    function goToCdSlide(idx) {
        const slider = document.getElementById('cdSlider');
        if(!slider) return;
        const isRTL = document.documentElement.getAttribute('dir') === 'rtl';
        slider.scrollTo({ left: isRTL ? -(idx * slider.clientWidth) : (idx * slider.clientWidth), behavior: 'smooth' });
    }
    function updateBdSliderDots() {
        const slider = document.getElementById('bdSlider');
        if(!slider) return;
        const thumbs = slider.parentElement.querySelectorAll('.slider-thumb');
        const idx = Math.round(Math.abs(slider.scrollLeft) / (slider.clientWidth || 1));
        thumbs.forEach((t, i) => t.classList.toggle('active', i === idx));
    }
    function goToBdSlide(idx) {
        const slider = document.getElementById('bdSlider');
        if(!slider) return;
        const isRTL = document.documentElement.getAttribute('dir') === 'rtl';
        slider.scrollTo({ left: isRTL ? -(idx * slider.clientWidth) : (idx * slider.clientWidth), behavior: 'smooth' });
    }

    // ================= REVIEW SUBMISSION =================
    function injectReviewForms() {
        document.querySelectorAll('.add-review-wrap').forEach(wrap => {
            if(wrap.dataset.injected === '1') return;
            wrap.dataset.injected = '1';
            const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
            wrap.innerHTML = `
                <div class="add-review-box">
                    <div class="add-review-head">
                        <h4 class="lang-text" data-ar="شاركنا تجربتك" data-en="Share your experience">شاركنا تجربتك</h4>
                        <p class="lang-text" data-ar="بنقبل تقييمات من العملاء اللي اشتروا المنتج فعلاً." data-en="We accept reviews from verified customers only.">بنقبل تقييمات من العملاء اللي اشتروا المنتج فعلاً.</p>
                    </div>
                    <div class="rate-stars" data-rating="0">
                        <span class="rs" data-v="1">★</span><span class="rs" data-v="2">★</span><span class="rs" data-v="3">★</span><span class="rs" data-v="4">★</span><span class="rs" data-v="5">★</span>
                    </div>
                    <textarea class="rev-textarea" rows="3" data-placeholder-ar="اكتب تجربتك مع المنتج (اختياري)..." data-placeholder-en="Tell us about your experience (optional)..." placeholder="اكتب تجربتك مع المنتج (اختياري)..."></textarea>
                    <button type="button" class="rev-submit lang-text" data-ar="انشر التقييم" data-en="POST REVIEW">انشر التقييم</button>
                    <div class="rev-msg"></div>
                </div>`;
            // Star interaction
            const stars = wrap.querySelectorAll('.rs');
            stars.forEach(s => {
                s.addEventListener('click', () => {
                    const v = parseInt(s.dataset.v);
                    wrap.querySelector('.rate-stars').dataset.rating = v;
                    stars.forEach(x => x.classList.toggle('active', parseInt(x.dataset.v) <= v));
                });
            });
            // Submit
            wrap.querySelector('.rev-submit').addEventListener('click', () => {
                const rating = parseInt(wrap.querySelector('.rate-stars').dataset.rating);
                const text = wrap.querySelector('.rev-textarea').value.trim();
                const msg = wrap.querySelector('.rev-msg');
                const isRtlNow = document.documentElement.getAttribute('dir') === 'rtl';
                if(!isLoggedIn) {
                    msg.innerHTML = `<span style="color:var(--red);">${isRtlNow ? 'لازم تكون مسجل دخول وتكون اشتريت المنتج عشان تقدر تقيّمه.' : 'You must be signed in and have purchased to leave a review.'} <a onclick="navigate('login')" style="color:var(--wood);text-decoration:underline;cursor:pointer;">${isRtlNow ? 'سجل الدخول' : 'Sign in'}</a></span>`;
                    return;
                }
                if(rating === 0) {
                    msg.innerHTML = `<span style="color:var(--red);">${isRtlNow ? 'اختار عدد النجوم أولاً' : 'Please pick a star rating first'}</span>`;
                    return;
                }
                // Append review
                const reviewsSection = wrap.closest('.reviews-section');
                if(reviewsSection) {
                    const card = document.createElement('div');
                    card.className = 'review-card';
                    const today = new Date();
                    const dateStr = String(today.getDate()).padStart(2, '0') + '/' + String(today.getMonth()+1).padStart(2, '0') + '/' + today.getFullYear();
                    const stars = '★'.repeat(rating) + '☆'.repeat(5 - rating);
                    // SECURITY: text is user input — must be escaped to prevent XSS
                    card.innerHTML = `
                        <div class="review-header">
                            <div class="review-name"><span>${isRtlNow ? 'أنت' : 'You'}</span><span class="verified-badge">✔</span></div>
                            <div class="review-date en-num">${dateStr}</div>
                        </div>
                        <div class="review-stars">${stars}</div>
                        <div class="review-text">${esc(text) || (isRtlNow ? '(بدون تعليق)' : '(No comment)')}</div>`;
                    reviewsSection.insertBefore(card, wrap.nextSibling);
                }
                msg.innerHTML = `<span style="color:var(--green);">${isRtlNow ? '✓ تم نشر تقييمك، شكراً!' : '✓ Your review was posted. Thank you!'}</span>`;
                wrap.querySelector('.rev-textarea').value = '';
                wrap.querySelectorAll('.rs').forEach(x => x.classList.remove('active'));
                wrap.querySelector('.rate-stars').dataset.rating = '0';
            });
        });
    }

    function addBundleToCart(btn) {
        const b = BUNDLES[currentBundleKey];
        if(!b) return;
        const qty = (typeof bundleQty === 'number' ? bundleQty : 1);
        addProductToCart({
            id: Date.now(),
            name: b.ar.title + ' (Bundle)',
            nameEn: b.en.title + ' (Bundle)',
            price: b.final, qty: qty, img: b.cover, volume: b.items.length + 'x ' + b.items[0].vol
        });
        addWithBottleAnim(btn);
    }

    // Add bundle directly from card without opening detail page
    function addBundleByKey(event, key) {
        if(event) { event.stopPropagation(); event.preventDefault(); }
        const btn = event ? event.currentTarget : null;
        const b = BUNDLES[key];
        if(!b) return;
        addProductToCart({
            id: Date.now(),
            name: b.ar.title + ' (Bundle)',
            nameEn: b.en.title + ' (Bundle)',
            price: b.final, qty: 1, img: b.cover, volume: b.items.length + 'x ' + b.items[0].vol
        });
        if(btn) addWithBottleAnim(btn);
    }

    function addCollectionToCart(btn) {
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        addProductToCart({
            id: Date.now(),
            name: 'مجموعة الاكتشاف الشاملة',
            nameEn: 'The Ultimate Discovery Set',
            price: 250, qty: 1,
            img: 'https://remal-perfume.runasp.net/disk1.webp',
            volume: '6 × 5 ML'
        });
        addWithBottleAnim(btn);
    }

    function toggleCdxWishlist(btn) {
        btn.classList.toggle('liked');
    }

    // ================= ORDER SUCCESS =================
    function copyOrderId(btn) {
        const id = document.getElementById('displayOrderId')?.innerText || '';
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        navigator.clipboard.writeText(id).then(() => {
            btn.classList.add('copied');
            const orig = btn.innerHTML;
            btn.innerHTML = `<svg viewBox="0 0 24 24" style="width:13px;height:13px;stroke:currentColor;fill:none;stroke-width:2.5;"><polyline points="20 6 9 17 4 12"/></svg><span>${isRtl ? 'تم!' : 'Copied!'}</span>`;
            setTimeout(() => { btn.classList.remove('copied'); btn.innerHTML = orig; }, 2000);
        }).catch(() => {
            const ta = Object.assign(document.createElement('textarea'), { value: id });
            document.body.appendChild(ta); ta.select(); document.execCommand('copy'); ta.remove();
            btn.classList.add('copied');
            setTimeout(() => btn.classList.remove('copied'), 2000);
        });
    }

    function trackFromSuccess() {
        const id = document.getElementById('displayOrderId')?.innerText || '';
        trackOrderById(id);
    }

    function trackOrderById(orderId) {
        navigate('tracking');
        // Pre-fill the input AND auto-submit so user doesn't re-enter the code
        setTimeout(() => {
            const input = document.getElementById('trackInput');
            if(input) input.value = orderId;
            if(typeof trackOrderNow === 'function') trackOrderNow();
        }, 50);
    }

    // ================= SAVED CHECKOUT INFO =================
    // Behavior:
    //   - Logged-in user with saved profile → AUTO-FILL silently on entering checkout
    //     (no banner, no click — just show a small "✓ تم استدعاء عنوانك المحفوظ" chip the user can edit any field)
    //   - Guest with saved sessionStorage → show clickable banner offering autofill
    function _applySavedShippingToForm(saved) {
        if (!saved) return false;
        const setEl = (id, val) => { const el = document.getElementById(id); if (el && val != null && val !== '') el.value = val; };
        if (typeof saved === 'object' && !Array.isArray(saved)) {
            setEl('shipFirstName',   saved.firstName);
            setEl('shipLastName',    saved.lastName);
            setEl('shipPhone',       saved.phone);
            setEl('shipEmail',       saved.email);
            // Advanced Matching لزائر عائد: بياناته المحفوظة بتعرّف البيكسل من
            // أول لحظة، فحتى لو ما أكملش الطلب بيتحسب في الجماهير بمطابقة عالية.
            try {
                window.RemalTrack.identify({
                    phone: saved.phone, firstName: saved.firstName, lastName: saved.lastName,
                    city: saved.governorate, externalId: saved.phone
                });
            } catch (e) {}
            setEl('shipGovernorate', saved.governorate);
            // مدن المحافظة بتتبني أولًا، وإلا قيمة المدينة المحفوظة مش هتلاقي خيار تتربط بيه
            if (typeof onGovernorateChange === 'function') onGovernorateChange();
            setEl('shipCity',        saved.city);
            setEl('shipAddress',     saved.address);
            return true;
        }
        if (Array.isArray(saved)) {
            setEl('shipFirstName', saved[0]); setEl('shipLastName', saved[1]);
            setEl('shipPhone', saved[2]); setEl('shipAddress', saved[3]); setEl('shipCity', saved[4]);
            return true;
        }
        return false;
    }
    function _showSavedAddressChip() {
        if (document.getElementById('savedAddrChip')) return;
        const step2 = document.getElementById('step2');
        const form = document.getElementById('shippingForm');
        const stepContent = step2 ? step2.querySelector('.step-content') : null;
        if (!stepContent || !form || form.parentNode !== stepContent) return;
        const chip = document.createElement('div');
        chip.id = 'savedAddrChip';
        chip.style.cssText = 'display:flex;align-items:center;justify-content:space-between;gap:10px;padding:10px 14px;margin-bottom:14px;background:#f0fdf4;border:1px solid #c8ecd0;border-radius:8px;font-size:13px;color:#1a6e34;';
        const isRtl = document.documentElement.dir === 'rtl';
        chip.innerHTML = ''
            + '<span style="display:flex;align-items:center;gap:8px;">'
            +   '<svg viewBox="0 0 24 24" style="width:16px;height:16px;stroke:#1a6e34;fill:none;stroke-width:2.5;flex-shrink:0;"><polyline points="20 6 9 17 4 12"/></svg>'
            +   '<span class="lang-text" data-ar="تم استدعاء عنوانك المحفوظ — تقدر تعدّل أي حقل" data-en="Your saved address has been loaded — feel free to edit any field">'
            +   (isRtl ? 'تم استدعاء عنوانك المحفوظ — تقدر تعدّل أي حقل' : 'Your saved address has been loaded — feel free to edit any field')
            +   '</span>'
            + '</span>';
        stepContent.insertBefore(chip, form);
    }
    function ensureSavedInfoBanner() {
        const form = document.getElementById('shippingForm');
        if (!form) return;
        const saved = (() => { try { return JSON.parse(sessionStorage.getItem('remal_saved_shipping') || 'null'); } catch (e) { return null; } })();
        const oldBanner = document.getElementById('savedInfoBanner');
        const oldChip   = document.getElementById('savedAddrChip');
        if (!saved) { if (oldBanner) oldBanner.remove(); if (oldChip) oldChip.remove(); return; }

        const loggedIn = (typeof API !== 'undefined' && API.isAuthed && API.isAuthed());
        const formIsEmpty = !(document.getElementById('shipFirstName') || {}).value;

        if (loggedIn && formIsEmpty) {
            // Auto-fill silently and show the green chip
            _applySavedShippingToForm(saved);
            const cb = document.getElementById('saveInfoCheck'); if (cb) cb.checked = true;
            _showSavedAddressChip();
            if (oldBanner) oldBanner.remove();
            return;
        }
        // Guest path — keep the click-to-autofill banner so they can opt in
        if (oldBanner) return;
        const banner = document.createElement('div');
        banner.id = 'savedInfoBanner';
        banner.setAttribute('role', 'button');
        banner.setAttribute('tabindex', '0');
        banner.style.cssText = 'display:flex;align-items:center;justify-content:space-between;gap:12px;padding:14px 18px;margin-bottom:18px;background:#fff8e6;border:1px solid #f5d77e;border-radius:8px;cursor:pointer;font-size:14px;';
        const isRtl = document.documentElement.dir === 'rtl';
        banner.innerHTML = ''
            + '<span style="display:flex;align-items:center;gap:10px;color:#5a4a10;">'
            +   '<svg viewBox="0 0 24 24" style="width:18px;height:18px;stroke:#8B6914;fill:none;stroke-width:2;"><polyline points="20 6 9 17 4 12"/></svg>'
            +   '<span class="lang-text" data-ar="عندنا بياناتك المحفوظة من المرة اللي فاتت — اضغط هنا لملء الفورم تلقائياً" data-en="We have your saved details — tap to autofill">'
            +   (isRtl ? 'عندنا بياناتك المحفوظة من المرة اللي فاتت — اضغط هنا لملء الفورم تلقائياً' : 'We have your saved details — tap to autofill')
            +   '</span>'
            + '</span>'
            + '<button type="button" id="savedInfoDismiss" aria-label="' + (isRtl ? 'إخفاء' : 'Dismiss') + '" style="background:transparent;border:none;font-size:18px;color:#5a4a10;cursor:pointer;line-height:1;padding:4px;">×</button>';
        const step2 = document.getElementById('step2');
        const stepContent = step2 ? step2.querySelector('.step-content') : null;
        if (stepContent && form.parentNode === stepContent) stepContent.insertBefore(banner, form);
        const applyAutofill = (e) => {
            if (e) { e.preventDefault(); e.stopPropagation(); }
            _applySavedShippingToForm(saved);
            const cb = document.getElementById('saveInfoCheck'); if (cb) cb.checked = true;
            toastMsg(t('تم ملء البيانات تلقائياً ✓', 'Details autofilled ✓'));
            banner.remove();
        };
        banner.addEventListener('click', applyAutofill);
        banner.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') applyAutofill(e); });
        const dismiss = banner.querySelector('#savedInfoDismiss');
        if (dismiss) dismiss.addEventListener('click', (e) => { e.stopPropagation(); banner.remove(); });
    }

    document.addEventListener("DOMContentLoaded", () => {
        // Show banner when checkout is rendered/navigated to (uses the navigate wrapper)
        ensureSavedInfoBanner();
        setupShippingValidation();
        // Persist shipping data on step 2 → 3 if the "save" checkbox is on; clear otherwise.
        // ===== أحداث Meta بعد إلغاء الخطوات =====
        // قبل كده InitiateCheckout كان بيتبعت لما العميل ينتقل للخطوة ٢، و
        // AddPaymentInfo عند الخطوة ٤. الخطوات دي اتشالت، فالحدثين اتربطوا
        // بأفعال حقيقية بيعملها العميل:
        //   InitiateCheckout → أول ما صفحة الدفع تتعرض ومعاها منتجات
        //   AddPaymentInfo   → أول ما يختار طريقة دفع فعليًا
        // كل واحد بيتبعت **مرة واحدة بس** لكل جلسة (علم على window) — التكرار
        // بيضخّم أرقام القمع في Ads Manager ويخرّب حساب تكلفة التحويل.
        function _checkoutItemsForTracking() {
            // getCheckoutItems() هي الطريقة الوحيدة الموثوقة للوصول للسلة من هنا —
            // المتغير نفسه معرّف بـ let في بلوك <script> تاني، فـ window.checkoutItems
            // بيرجع undefined دايمًا (اتأكدنا من ده بالقياس على الموقع الحيّ).
            var src = (typeof window.getCheckoutItems === 'function' ? window.getCheckoutItems() : null)
                   || window.checkoutItems || [];
            return src.map(function (i) {
                return { id: i.productId || i.bundleId || i.collectionId, name: i.nameEn || i.name,
                         variant: i.volume, price: i.price, quantity: i.qty };
            });
        }

        window.trackBeginCheckout = function () {
            if (window.__trackedBeginCheckout) return;
            var items = _checkoutItemsForTracking();
            if (!items.length) return;              // سلة فاضية = مش بداية دفع
            window.__trackedBeginCheckout = true;
            try { window.RemalTrack.event('begin_checkout', { items: items }); } catch (e) {}
        };

        window.trackAddPaymentInfo = function (method) {
            if (window.__trackedPaymentInfo) return;
            var items = _checkoutItemsForTracking();
            if (!items.length) return;
            window.__trackedPaymentInfo = true;
            try {
                window.RemalTrack.event('add_payment_info', {
                    items: items,
                    method: method || (typeof selectedPayment !== 'undefined' ? selectedPayment : '')
                });
            } catch (e) {}
        };

        // بيتنادى من الراوتر عند عرض صفحة الدفع. بنأجّله لحد ما السلة تتحمّل —
        // لو بعتناه والسلة لسه فاضية الحدث بيروح لميتا من غير محتويات ولا قيمة،
        // وده بيخرّب مطابقة الكتالوج وحساب القيمة في الإعلانات.
        window.onCheckoutShown = function () {
            var tries = 0;
            (function attempt() {
                if (window.__trackedBeginCheckout) return;
                if (_checkoutItemsForTracking().length) { window.trackBeginCheckout(); return; }
                if (++tries > 20) return;           // ٤ ثواني كحد أقصى ثم نسيب
                setTimeout(attempt, 200);
            })();
        };
    });
    // Refresh the banner every time the user navigates back to checkout
    document.addEventListener('remal:navigated', (e) => {
        if (e && e.detail === 'checkout') { ensureSavedInfoBanner(); setupShippingValidation(); }
    });

    // ================= LIVE PER-FIELD SHIPPING VALIDATION (Fix 4) =================
    // Validate as the user types/blurs each field. Shows an inline error message
    // under the field and a red border. Blocks step 2 → 3 if anything is invalid.
    (function injectValidationStyles() {
        if (document.getElementById('shipValStyles')) return;
        const st = document.createElement('style');
        st.id = 'shipValStyles';
        st.textContent = ''
            + '.luxury-input.ship-invalid{border-color:#d33!important;background:#fff5f5!important;}'
            + '.luxury-input.ship-valid{border-color:#3a9d4f!important;}'
            + '.ship-err-msg{display:block;color:#d33;font-size:12px;margin-top:-6px;margin-bottom:8px;line-height:1.4;}'
            + '.ship-err-msg:empty{display:none;}';
        document.head.appendChild(st);
    })();

    function _validateShippingField(input) {
        // حقل مخفي (مثلًا المدينة لمحافظة مالهاش مدن) مايتحققش منه ومايعطلش الطلب
        if (input.offsetParent === null && input.closest && input.closest('[hidden]')) {
            input.classList.remove('ship-invalid', 'ship-valid');
            return true;
        }
        const v = (input.value || '').trim();
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        let err = '';
        switch (input.id) {
            case 'shipFirstName':
                if (!v) err = isRtl ? 'اكتب اسمك الأول' : 'Please enter your first name';
                else if (v.length < 2) err = isRtl ? 'الاسم قصير جداً' : 'Name is too short';
                break;
            case 'shipLastName':
                if (!v) err = isRtl ? 'اكتب اسم العائلة' : 'Please enter your last name';
                else if (v.length < 2) err = isRtl ? 'الاسم قصير جداً' : 'Name is too short';
                break;
            case 'shipPhone': {
                if (!v) { err = isRtl ? 'اكتب رقم الموبايل' : 'Please enter your phone number'; break; }
                const digits = v.replace(/[\s\-]/g, '');
                if (!/^01[0-9]{9}$/.test(digits)) {
                    err = isRtl ? 'رقم موبايل غير صحيح — لازم يبدأ بـ 01 و11 رقم' : 'Invalid mobile — must start with 01 and be 11 digits';
                }
                break;
            }
            case 'shipEmail':
                // اختياري بالكامل: فاضي = عدّي. لو اتكتب لازم يكون بريدًا صحيحًا،
                // وإلا تأكيد الطلب هيروح لعنوان غلط والعميل هيفتكر إننا ما بعتناش.
                if (v && !/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v)) {
                    err = isRtl ? 'البريد الإلكتروني مش مظبوط' : 'Invalid email address';
                }
                break;
            case 'shipGovernorate':
                if (!v) err = isRtl ? 'اختار المحافظة' : 'Please select your governorate';
                break;
            case 'shipCity':
                // المدينة إلزامية فقط لما المحافظة تكون متضافلها مدن (الحقل ظاهر)
                if (!v) err = isRtl ? 'اختار المدينة' : 'Please select your city';
                break;
            case 'shipAddress':
                if (!v) err = isRtl ? 'اكتب العنوان بالتفصيل' : 'Please enter your detailed address';
                else if (v.length < 10) err = isRtl ? 'العنوان مختصر جداً — اكتب الشارع ورقم العمارة' : 'Too short — include street and building number';
                break;
        }
        // Apply styling + message
        let msgEl = input.nextElementSibling;
        if (!msgEl || !msgEl.classList || !msgEl.classList.contains('ship-err-msg')) {
            msgEl = document.createElement('span');
            msgEl.className = 'ship-err-msg';
            input.parentNode.insertBefore(msgEl, input.nextSibling);
        }
        if (err) {
            input.classList.add('ship-invalid');
            input.classList.remove('ship-valid');
            msgEl.textContent = err;
        } else {
            input.classList.remove('ship-invalid');
            input.classList.add('ship-valid');
            msgEl.textContent = '';
        }
        return !err;
    }

    function validateShippingForm(opts) {
        opts = opts || {};
        const ids = ['shipFirstName', 'shipLastName', 'shipPhone', 'shipEmail', 'shipGovernorate', 'shipCity', 'shipAddress'];
        let firstBad = null;
        let allOk = true;
        ids.forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            const ok = _validateShippingField(el);
            if (!ok) { allOk = false; if (!firstBad) firstBad = el; }
        });
        if (!allOk && opts.focusFirstError && firstBad) {
            try { firstBad.focus({ preventScroll: false }); } catch (e) { firstBad.focus(); }
            try { firstBad.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch (e) {}
        }
        return allOk;
    }
    window.validateShippingForm = validateShippingForm;

    // ================= LIVE PER-FIELD VALIDATION FOR LOGIN + REGISTER =================
    (function injectAuthValStyles() {
        if (document.getElementById('authValStyles')) return;
        const st = document.createElement('style');
        st.id = 'authValStyles';
        st.textContent = ''
            + '.auth-form-group input.auth-invalid, .auth-form-group select.auth-invalid { border-color:#d33!important; background:#fff5f5!important; }'
            + '.auth-form-group input.auth-valid, .auth-form-group select.auth-valid { border-color:#3a9d4f!important; }'
            + '.auth-err-msg { display:block; color:#d33; font-size:12px; margin-top:4px; line-height:1.4; }'
            + '.auth-err-msg:empty { display:none; }';
        document.head.appendChild(st);
    })();

    function _setAuthFieldError(input, err) {
        if (!input) return !err;
        let msgEl = input.parentNode && input.parentNode.querySelector(':scope > .auth-err-msg');
        if (!msgEl) {
            msgEl = document.createElement('span');
            msgEl.className = 'auth-err-msg';
            input.parentNode.appendChild(msgEl);
        }
        if (err) {
            input.classList.add('auth-invalid');
            input.classList.remove('auth-valid');
            msgEl.textContent = err;
            return false;
        }
        input.classList.remove('auth-invalid');
        if ((input.value || '').trim()) input.classList.add('auth-valid');
        else input.classList.remove('auth-valid');
        msgEl.textContent = '';
        return true;
    }

    function _validateLoginField(input) {
        const v = (input.value || '').trim();
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        if (input.id === 'loginUser') {
            if (!v) return _setAuthFieldError(input, isRtl ? 'اكتب الإيميل أو رقم الموبايل' : 'Enter your email or phone');
            // Either valid email OR Egyptian mobile
            const isEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v);
            const isPhone = /^01[0-9]{9}$/.test(v.replace(/[\s-]/g, ''));
            if (!isEmail && !isPhone) return _setAuthFieldError(input, isRtl ? 'إيميل أو رقم موبايل غير صحيح' : 'Invalid email or mobile');
            return _setAuthFieldError(input, '');
        }
        if (input.id === 'loginPass') {
            if (!v) return _setAuthFieldError(input, isRtl ? 'اكتب الباسورد' : 'Enter your password');
            return _setAuthFieldError(input, '');
        }
        return true;
    }

    function _validateRegisterField(input) {
        const v = (input.value || '').trim();
        const isRtl = document.documentElement.getAttribute('dir') === 'rtl';
        switch (input.id) {
            case 'regFirstName':
            case 'regLastName':
                if (!v) return _setAuthFieldError(input, isRtl ? 'مطلوب' : 'Required');
                if (v.length < 2) return _setAuthFieldError(input, isRtl ? 'الاسم قصير جداً' : 'Name is too short');
                return _setAuthFieldError(input, '');
            case 'regPhone': {
                if (!v) return _setAuthFieldError(input, isRtl ? 'اكتب رقم الموبايل' : 'Enter mobile number');
                const cleaned = v.replace(/[\s-]/g, '');
                if (!/^01[0-9]{9}$/.test(cleaned)) return _setAuthFieldError(input, isRtl ? 'رقم موبايل غير صحيح — لازم يبدأ بـ 01 و11 رقم' : 'Must start with 01 and be 11 digits');
                return _setAuthFieldError(input, '');
            }
            case 'regEmail':
                if (!v) return _setAuthFieldError(input, isRtl ? 'اكتب الإيميل' : 'Enter your email');
                if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) return _setAuthFieldError(input, isRtl ? 'إيميل غير صحيح' : 'Invalid email');
                return _setAuthFieldError(input, '');
            case 'regPassword':
                if (!v) return _setAuthFieldError(input, isRtl ? 'اكتب الباسورد' : 'Enter a password');
                if (v.length < 8) return _setAuthFieldError(input, isRtl ? 'الباسورد لازم ٨ حروف على الأقل' : 'Password must be at least 8 characters');
                if (!/[A-Za-z]/.test(v) || !/[0-9]/.test(v)) return _setAuthFieldError(input, isRtl ? 'لازم يحتوي على حروف وأرقام' : 'Must contain letters and numbers');
                // Cross-validate confirm if it has a value already
                const cf = document.getElementById('regConfirmPass');
                if (cf && cf.value && cf.value !== v) _setAuthFieldError(cf, isRtl ? 'الباسورد مش متطابق' : 'Passwords do not match');
                else if (cf && cf.value === v) _setAuthFieldError(cf, '');
                return _setAuthFieldError(input, '');
            case 'regConfirmPass': {
                if (!v) return _setAuthFieldError(input, isRtl ? 'أكد الباسورد' : 'Confirm your password');
                const p = (document.getElementById('regPassword') || {}).value || '';
                if (v !== p) return _setAuthFieldError(input, isRtl ? 'الباسورد مش متطابق' : 'Passwords do not match');
                return _setAuthFieldError(input, '');
            }
        }
        return true;
    }

    window.validateLoginForm = function () {
        let ok = true;
        ['loginUser', 'loginPass'].forEach(id => { const el = document.getElementById(id); if (el && !_validateLoginField(el)) ok = false; });
        return ok;
    };
    window.validateRegisterForm = function () {
        let ok = true;
        ['regFirstName', 'regLastName', 'regPhone', 'regEmail', 'regPassword', 'regConfirmPass'].forEach(id => {
            const el = document.getElementById(id); if (el && !_validateRegisterField(el)) ok = false;
        });
        return ok;
    };

    function setupAuthValidation() {
        ['loginUser', 'loginPass'].forEach(id => {
            const el = document.getElementById(id);
            if (!el || el.dataset.liveValBound === '1') return;
            el.addEventListener('blur', () => _validateLoginField(el));
            el.addEventListener('input', () => { if (el.classList.contains('auth-invalid')) _validateLoginField(el); });
            el.dataset.liveValBound = '1';
        });
        ['regFirstName', 'regLastName', 'regPhone', 'regEmail', 'regPassword', 'regConfirmPass'].forEach(id => {
            const el = document.getElementById(id);
            if (!el || el.dataset.liveValBound === '1') return;
            el.addEventListener('blur', () => _validateRegisterField(el));
            el.addEventListener('input', () => { if (el.classList.contains('auth-invalid')) _validateRegisterField(el); });
            if (id === 'regPhone') {
                el.addEventListener('input', () => {
                    const cleaned = el.value.replace(/[^\d]/g, '');
                    if (cleaned !== el.value) el.value = cleaned;
                });
            }
            el.dataset.liveValBound = '1';
        });
    }
    window.setupAuthValidation = setupAuthValidation;
    document.addEventListener('DOMContentLoaded', setupAuthValidation);
    document.addEventListener('remal:navigated', (e) => {
        if (e && (e.detail === 'login' || e.detail === 'register')) setupAuthValidation();
    });

    function setupShippingValidation() {
        const form = document.getElementById('shippingForm');
        if (!form || form.dataset.liveValBound === '1') return;
        const fields = form.querySelectorAll('input, select');
        fields.forEach(el => {
            // Skip the saveInfoLabel + other unrelated controls if any
            if (!el.id || !el.id.startsWith('ship')) return;
            // Validate on blur / change
            const evt = el.tagName === 'SELECT' ? 'change' : 'blur';
            el.addEventListener(evt, () => _validateShippingField(el));
            // While typing: only clear the error once the value becomes valid
            el.addEventListener('input', () => {
                if (el.classList.contains('ship-invalid')) _validateShippingField(el);
            });
            // For the phone field, strip non-digits as user types
            if (el.id === 'shipPhone') {
                el.addEventListener('input', () => {
                    const cleaned = el.value.replace(/[^\d]/g, '');
                    if (cleaned !== el.value) el.value = cleaned;
                });
            }
        });
        form.dataset.liveValBound = '1';
    }
    window.setupShippingValidation = setupShippingValidation;

    // ================= TICKER NORMALIZER =================
    // New 3-item tickers (those marked with `data-no-normalize="1"`) are left alone — they
    // already have exactly 3 unique lines + 1 tail duplicate, and use the flipVertical3 animation.
    // Legacy 9-item tickers in hardcoded HTML still get padded for the flipVertical9 animation.
    function normalizeTickers() {
        const TARGET = 9;
        document.querySelectorAll('.vertical-ticker').forEach(ticker => {
            if (ticker.dataset.noNormalize === '1' || ticker.classList.contains('vt-3items')) return;
            const items = Array.from(ticker.querySelectorAll('.ticker-item'));
            if(items.length === 0) return;
            if(ticker.dataset.normalized === '1') return;
            const base = items.slice(0, TARGET);
            while(base.length < TARGET) base.push(items[base.length % items.length].cloneNode(true));
            const tail = base[0].cloneNode(true);
            ticker.innerHTML = '';
            base.forEach(it => ticker.appendChild(it.cloneNode ? it.cloneNode(true) : it));
            ticker.appendChild(tail);
            ticker.dataset.normalized = '1';
        });
    }

    // ================= INIT =================
    document.addEventListener("DOMContentLoaded", () => {
        updateWishlistBadge();
        initHeartIcons();
        updateAuthUI();
        syncMnavLangButtons();
        normalizeTickers();
        injectReviewForms();
        // شبكة أمان: نفضّي كروت الـ placeholder الثابتة من صفحات القوائم الكاملة فورًا،
        // عشان لو حصل أي خلل في التحميل ميظهرش للعميل منتجات وهمية قديمة إطلاقًا.
        // (الرندرر بيملاها بالسكيلتون ثم الداتا الحقيقية عند فتح الصفحة).
        ['catalogGrid', 'bundlesGrid', 'collectionsPageGrid'].forEach(id => {
            const g = document.getElementById(id);
            if (g) g.innerHTML = '';
        });
    });

    // Run again after navigations (in case section was hidden during initial run)
    const _origNavigateForTicker = navigate;
    navigate = function(p) {
        const r = _origNavigateForTicker(p);
        setTimeout(() => {
            normalizeTickers();
            if(typeof injectReviewForms === 'function') injectReviewForms();
        }, 50);
        return r;
    };

