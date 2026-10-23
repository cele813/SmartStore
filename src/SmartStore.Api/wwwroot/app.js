'use strict';
const $ = id => document.getElementById(id);
const state = { user: null, csrf: null, mode: 'login', view: 'overview', page: 1, salesPage: 1, categories: [], suppliers: [], products: [], sales: [], saleProducts: [], totalProducts: 0, totalSales: 0, editor: null, loading: false };
const money = value => new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(value);
const date = value => new Date(value).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const empty = message => `<div class="empty">${escapeHtml(message)}</div>`;
const titles = { overview: ['A clear view of your store.', 'A good day starts with everything in one place.'], products: ['A place for every product.', 'Manage your catalog and keep inventory in view.'], categories: ['Good things, well organized.', 'Give every product a place in your catalog.'], suppliers: ['Better business, together.', 'Keep your supplier details close at hand.'], sales: ['Every sale, accounted for.', 'Record orders and keep your stock in sync.'] };

async function refreshCsrf() {
    const response = await fetch('/api/auth/csrf', { credentials: 'same-origin', cache: 'no-store' });
    if (!response.ok) throw new Error('Could not connect. Make sure the API is running.');
    state.csrf = (await response.json()).token;
}
async function api(path, options = {}) {
    const method = options.method || 'GET';
    const headers = { ...options.headers };
    if (!['GET', 'HEAD'].includes(method)) {
        if (!state.csrf) await refreshCsrf();
        headers['X-CSRF-TOKEN'] = state.csrf;
    }
    if (options.body !== undefined) headers['Content-Type'] = 'application/json';
    const response = await fetch(path, { ...options, method, headers, credentials: 'same-origin', cache: 'no-store', body: options.body === undefined ? undefined : JSON.stringify(options.body) });
    const content = response.status === 204 ? null : await response.json().catch(() => null);
    if (!response.ok) {
        if (response.status === 401 && state.user && !path.includes('/auth/login')) {
            state.user = null; state.csrf = null;
            document.querySelectorAll('dialog[open]').forEach(dialog => dialog.close());
            showAuth();
        }
        const message = content?.errors ? Object.values(content.errors).flat().join(' ') : content?.title;
        throw new Error(message || ({ 401: 'Please sign in to continue.', 400: 'The request was not accepted. Check the fields and try again.', 429: 'Too many attempts. Please wait a minute and try again.' }[response.status]) || 'Request failed. Please try again.');
    }
    return content;
}
function errorAt(id, error) { $(id).textContent = error.message; $(id).hidden = false; }
let toastTimer;
function toast(message) { $('toast').textContent = message; $('toast').hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').hidden = true, 3500); }
function showAuth() { window.scrollTo(0, 0); $('auth-view').hidden = false; $('app-view').hidden = true; $('auth-form').reset(); }
function setMode(mode) {
    state.mode = mode;
    const register = mode === 'register';
    $('name-field').hidden = !register; $('display-name').required = register;
    $('password-help').hidden = !register; $('password').minLength = register ? 8 : 1;
    $('password').autocomplete = register ? 'new-password' : 'current-password';
    $('auth-title').textContent = register ? 'Your next chapter starts here.' : 'Welcome back.';
    $('auth-subtitle').textContent = register ? 'Create your account and step into your workspace.' : 'Sign in to pick up where you left off.';
    $('auth-submit').textContent = register ? 'Create account ↗' : 'Sign in ↗';
    $('login-tab').classList.toggle('selected', !register); $('register-tab').classList.toggle('selected', register);
    $('auth-error').hidden = true;
}
function showWorkspace() {
    window.scrollTo(0, 0);
    $('auth-view').hidden = true; $('app-view').hidden = false;
    $('user-name').textContent = state.user.displayName; $('user-email').textContent = state.user.email;
    $('avatar').textContent = state.user.displayName.slice(0, 1).toUpperCase();
}
async function load() {
    if (state.loading) return;
    state.loading = true; $('refresh').disabled = true; $('page-error').hidden = true;
    try {
        const [categories, suppliers, products, sales] = await Promise.all([
            api('/api/categories'), api('/api/suppliers'), api(`/api/products?page=${state.page}&pageSize=10`), api(`/api/sales?page=${state.salesPage}&pageSize=10`)
        ]);
        state.categories = categories; state.suppliers = suppliers;
        state.products = products.items; state.totalProducts = products.total;
        state.sales = sales.items; state.totalSales = sales.total;
        render();
    } catch (error) { errorAt('page-error', error); }
    finally { state.loading = false; $('refresh').disabled = false; }
}
function navigate(view) {
    state.view = view;
    for (const name of Object.keys(titles)) $(`${name}-section`).hidden = name !== view;
    document.querySelectorAll('.nav-item').forEach(button => button.classList.toggle('active', button.dataset.view === view));
    $('breadcrumb').textContent = view[0].toUpperCase() + view.slice(1);
    $('page-title').textContent = titles[view][0]; $('page-description').textContent = titles[view][1];
}
function render() {
    $('stat-products').textContent = state.totalProducts; $('stat-categories').textContent = state.categories.length;
    $('stat-suppliers').textContent = state.suppliers.length; $('stat-sales').textContent = state.totalSales;
    $('recent-sales').innerHTML = state.sales.length ? state.sales.slice(0, 4).map(order => `<div class="recent-row"><div><strong>Order #${escapeHtml(order.id.slice(0, 8))}</strong><small>${escapeHtml(date(order.createdAt))}</small></div><strong>${money(order.total)} ₫</strong></div>`).join('') : empty('No sales yet. Your first order starts a new chapter.');
    $('products-body').innerHTML = state.products.length ? state.products.map(product => `<tr><td><strong>${escapeHtml(product.name)}</strong><small>${escapeHtml(product.sku)}</small></td><td>${escapeHtml(state.categories.find(x => x.id === product.categoryId)?.name || '—')}</td><td>${money(product.price)}</td><td><span class="badge ${product.stock < 5 ? 'low' : ''}">${product.stock} units</span></td><td><div class="row-actions"><button data-action="stock" data-id="${product.id}">Stock</button><button data-action="history" data-id="${product.id}">History</button><button data-action="edit-product" data-id="${product.id}">Edit</button><button class="danger" data-action="delete-product" data-id="${product.id}">Delete</button></div></td></tr>`).join('') : '<tr><td colspan="5">' + empty('No products yet. Add a category, then your first product.') + '</td></tr>';
    $('categories-list').innerHTML = state.categories.length ? state.categories.map(item => `<div class="record-row"><div><strong>${escapeHtml(item.name)}</strong></div><div class="row-actions"><button data-action="edit-category" data-id="${item.id}">Edit</button><button class="danger" data-action="delete-category" data-id="${item.id}">Delete</button></div></div>`).join('') : empty('Start with a category to organize your products.');
    $('suppliers-list').innerHTML = state.suppliers.length ? state.suppliers.map(item => `<div class="record-row"><div><strong>${escapeHtml(item.name)}</strong><p>${escapeHtml([item.email, item.phone].filter(Boolean).join(' · ') || 'No contact details')}</p></div><div class="row-actions"><button data-action="edit-supplier" data-id="${item.id}">Edit</button><button class="danger" data-action="delete-supplier" data-id="${item.id}">Delete</button></div></div>`).join('') : empty('Add a supplier to keep your business contacts together.');
    $('sales-body').innerHTML = state.sales.length ? state.sales.map(order => `<tr><td><strong>#${escapeHtml(order.id.slice(0, 8))}</strong></td><td>${escapeHtml(date(order.createdAt))}</td><td>${order.items.reduce((sum, item) => sum + item.quantity, 0)}</td><td>${money(order.total)}</td><td><div class="row-actions"><button data-action="sale-detail" data-id="${order.id}">View order ↗</button></div></td></tr>`).join('') : '<tr><td colspan="5">' + empty('No orders yet. Create your first sale when stock is ready.') + '</td></tr>';
    $('product-page-info').textContent = `${state.totalProducts} products · Page ${state.page}`;
    $('sale-page-info').textContent = `${state.totalSales} orders · Page ${state.salesPage}`;
    $('previous-products').disabled = state.page <= 1; $('next-products').disabled = state.page * 10 >= state.totalProducts;
    $('previous-sales').disabled = state.salesPage <= 1; $('next-sales').disabled = state.salesPage * 10 >= state.totalSales;
}
function field(label, name, value = '', options = '') { return `<label>${label}<input name="${name}" value="${escapeHtml(value)}" ${options}></label>`; }
function select(label, name, items, selected, optional = false) {
    return `<label>${label}<select name="${name}" ${optional ? '' : 'required'}><option value="">${optional ? 'None' : 'Select ' + label.toLowerCase()}</option>${items.map(item => `<option value="${item.id}" ${selected === item.id ? 'selected' : ''}>${escapeHtml(item.name)}</option>`).join('')}</select></label>`;
}
function openEditor(kind, item) {
    $('editor-form').reset(); $('editor-error').hidden = true; $('editor-submit').disabled = false;
    state.editor = { kind, item };
    const noun = { product: 'product', category: 'category', supplier: 'supplier', stock: 'stock adjustment', sale: 'sale' }[kind];
    $('editor-title').textContent = kind === 'stock' ? `Adjust stock · ${item.name}` : `${item ? 'Edit' : 'New'} ${noun}`;
    $('editor-submit').textContent = kind === 'sale' ? 'Complete sale' : 'Save';
    let fields = '';
    if (kind === 'category') fields = field('Category name', 'name', item?.name, 'required maxlength="100"');
    if (kind === 'supplier') fields = field('Supplier name', 'name', item?.name, 'required maxlength="200"') + '<div class="form-grid">' + field('Email', 'email', item?.email, 'type="email" maxlength="254"') + field('Phone', 'phone', item?.phone, 'type="tel" maxlength="30"') + '</div>';
    if (kind === 'product') fields = field('Product name', 'name', item?.name, 'required maxlength="200"') + '<div class="form-grid">' + field('SKU', 'sku', item?.sku, 'required maxlength="50"') + field('Price (VND)', 'price', item?.price ?? 0, 'type="number" min="0" max="9999999999999999.99" step="0.01" required') + '</div>' + select('Category', 'categoryId', state.categories, item?.categoryId) + select('Supplier', 'supplierId', state.suppliers, item?.supplierId, true) + '<p class="hint">New products start with zero stock. Use Stock to receive inventory.</p>';
    if (kind === 'stock') fields = `<p class="muted">Current stock: ${item.stock}. Use a positive quantity to receive stock, or a negative quantity to deduct it.</p>` + field('Quantity change', 'quantity', '', 'type="number" step="1" min="-2147483647" max="2147483647" required') + field('Reason', 'reason', '', 'required maxlength="300" placeholder="e.g. Initial stock receipt"');
    if (kind === 'sale') fields = '<p class="muted small">Choose products and quantities. Prices are taken from your catalog.</p><div id="sale-lines"></div><button type="button" class="secondary compact" id="add-sale-line">+ Add line</button><div class="sale-total"><span>Total (VND)</span><span id="sale-total">0</span></div>';
    $('editor-fields').innerHTML = fields;
    if (kind === 'sale') { addSaleLine(); $('add-sale-line').addEventListener('click', addSaleLine); }
    $('editor-dialog').showModal();
}
function addSaleLine() {
    if ($('sale-lines').children.length >= 100) return;
    const row = document.createElement('div'); row.className = 'sale-line';
    row.innerHTML = `<select name="saleProduct" aria-label="Product" required><option value="">Select product</option>${state.saleProducts.filter(item => item.stock > 0).map(item => `<option value="${item.id}">${escapeHtml(item.name)} (${item.stock} in stock)</option>`).join('')}</select><input name="saleQuantity" aria-label="Quantity" type="number" min="1" max="2147483647" step="1" value="1" required><button type="button" aria-label="Remove line">×</button>`;
    row.querySelector('button').addEventListener('click', () => { if ($('sale-lines').children.length > 1) { row.remove(); updateSaleTotal(); } });
    row.addEventListener('input', updateSaleTotal); $('sale-lines').append(row);
}
function updateSaleTotal() {
    let total = 0;
    document.querySelectorAll('.sale-line').forEach(row => { total += (state.saleProducts.find(item => item.id === row.querySelector('select').value)?.price || 0) * Number(row.querySelector('input').value); });
    $('sale-total').textContent = money(total);
}
async function newSale() {
    $('add-sale').disabled = true;
    try {
        const products = [];
        let page = 1, result;
        do { result = await api(`/api/products?page=${page++}&pageSize=100`); products.push(...result.items); } while (products.length < result.total);
        state.saleProducts = products;
        if (!products.some(item => item.stock > 0)) { toast('Receive stock for a product before creating a sale.'); return; }
        openEditor('sale');
    } catch (error) { errorAt('page-error', error); }
    finally { $('add-sale').disabled = false; }
}
async function detail(kind, id) {
    $('detail-title').textContent = kind === 'history' ? 'Stock movements' : 'Order details';
    $('detail-content').innerHTML = empty('Loading…'); $('detail-dialog').showModal();
    try {
        if (kind === 'history') {
            const items = await api(`/api/products/${id}/stock-movements`);
            $('detail-content').innerHTML = items.length ? items.map(item => `<div class="detail-item"><div><strong>${escapeHtml(item.reason)}</strong><small>${escapeHtml(date(item.createdAt))}</small></div><strong>${item.quantity > 0 ? '+' : ''}${item.quantity}</strong></div>`).join('') : empty('No stock movements yet.');
        } else {
            const order = await api(`/api/sales/${id}`);
            const items = await Promise.all(order.items.map(async item => ({ ...item, product: await api(`/api/products/${item.productId}`) })));
            $('detail-content').innerHTML = `<p class="muted small">${escapeHtml(order.id)}<br>${escapeHtml(date(order.createdAt))}</p>` + items.map(item => `<div class="detail-item"><div><strong>${escapeHtml(item.product.name)}</strong><small>${item.quantity} × ${money(item.unitPrice)} ₫</small></div><strong>${money(item.quantity * item.unitPrice)} ₫</strong></div>`).join('') + `<div class="sale-total"><span>Total</span><span>${money(order.total)} ₫</span></div>`;
        }
    } catch (error) { $('detail-content').textContent = error.message; }
}
$('auth-form').addEventListener('submit', async event => {
    event.preventDefault(); $('auth-error').hidden = true; $('auth-submit').disabled = true;
    try {
        await refreshCsrf();
        const data = Object.fromEntries(new FormData(event.target));
        state.user = await api(`/api/auth/${state.mode}`, { method: 'POST', body: data });
        await refreshCsrf(); $('auth-form').reset(); state.page = 1; state.salesPage = 1;
        showWorkspace(); navigate('overview'); await load();
    } catch (error) { errorAt('auth-error', error); }
    finally { $('auth-submit').disabled = false; }
});
$('editor-form').addEventListener('submit', async event => {
    event.preventDefault(); $('editor-error').hidden = true; $('editor-submit').disabled = true;
    try {
        const { kind, item } = state.editor;
        const data = Object.fromEntries(new FormData(event.target));
        let path, method = item ? 'PUT' : 'POST', body = data;
        if (kind === 'product') { path = '/api/products'; body.price = Number(data.price); body.supplierId = data.supplierId || null; }
        if (kind === 'category') path = '/api/categories';
        if (kind === 'supplier') { path = '/api/suppliers'; body.email = data.email || null; body.phone = data.phone || null; }
        if (item) path += `/${item.id}`;
        if (kind === 'stock') {
            path = `/api/products/${item.id}/stock-adjustments`; method = 'POST'; body = { quantity: Number(data.quantity), reason: data.reason };
            if (!body.quantity) throw new Error('Quantity must not be zero.');
        }
        if (kind === 'sale') {
            path = '/api/sales'; method = 'POST';
            body = { items: [...document.querySelectorAll('.sale-line')].map(row => ({ productId: row.querySelector('select').value, quantity: Number(row.querySelector('input').value) })) };
            if (new Set(body.items.map(line => line.productId)).size !== body.items.length) throw new Error('Each product can appear only once in an order.');
        }
        await api(path, { method, body }); $('editor-dialog').close(); toast(kind === 'sale' ? 'Sale recorded. Inventory updated.' : 'Saved successfully.'); await load();
    } catch (error) { errorAt('editor-error', error); }
    finally { $('editor-submit').disabled = false; }
});
document.addEventListener('click', async event => {
    const navigation = event.target.closest('[data-view]');
    if (navigation) { navigate(navigation.dataset.view); return; }
    if (event.target.closest('.close-dialog')) { event.target.closest('dialog').close(); return; }
    const action = event.target.closest('[data-action]');
    if (!action) return;
    const { id } = action.dataset;
    const [verb, kind] = action.dataset.action.split('-');
    try {
        if (verb === 'stock') return openEditor('stock', state.products.find(item => item.id === id));
        if (verb === 'history') return await detail('history', id);
        if (verb === 'sale') return await detail('sale', id);
        const collections = { product: state.products, category: state.categories, supplier: state.suppliers };
        const item = collections[kind].find(item => item.id === id);
        if (verb === 'edit') return openEditor(kind, item);
        if (verb === 'delete') {
            if (!confirm(`Delete "${item.name}"? Records linked to products or sales history cannot be deleted.`)) return;
            action.disabled = true;
            const resources = { product: 'products', category: 'categories', supplier: 'suppliers' };
            await api(`/api/${resources[kind]}/${id}`, { method: 'DELETE' }); toast('Record deleted.');
            if (kind === 'product' && state.products.length === 1 && state.page > 1) state.page--;
            await load();
        }
    } catch (error) { errorAt('page-error', error); }
    finally { action.disabled = false; }
});
$('login-tab').addEventListener('click', () => setMode('login'));
$('register-tab').addEventListener('click', () => setMode('register'));
$('refresh').addEventListener('click', load);
$('add-category').addEventListener('click', () => openEditor('category'));
$('add-supplier').addEventListener('click', () => openEditor('supplier'));
$('add-product').addEventListener('click', () => { if (!state.categories.length) { toast('Create a category before adding a product.'); navigate('categories'); return; } openEditor('product'); });
$('add-sale').addEventListener('click', newSale);
$('previous-products').addEventListener('click', async () => { state.page--; await load(); });
$('next-products').addEventListener('click', async () => { state.page++; await load(); });
$('previous-sales').addEventListener('click', async () => { state.salesPage--; await load(); });
$('next-sales').addEventListener('click', async () => { state.salesPage++; await load(); });
$('logout').addEventListener('click', async () => {
    $('logout').disabled = true;
    try { await api('/api/auth/logout', { method: 'POST' }); state.user = null; state.csrf = null; showAuth(); setMode('login'); await refreshCsrf(); }
    catch (error) { errorAt('page-error', error); }
    finally { $('logout').disabled = false; }
});
(async function start() {
    $('auth-submit').disabled = true;
    try {
        await refreshCsrf();
        const response = await fetch('/api/auth/me', { credentials: 'same-origin', cache: 'no-store' });
        if (response.ok) { state.user = await response.json(); showWorkspace(); await load(); }
        else if (response.status !== 401) throw new Error('Could not load your account. Check the database connection and migrations.');
    } catch (error) { errorAt('auth-error', error); }
    finally { $('auth-submit').disabled = false; }
})();
