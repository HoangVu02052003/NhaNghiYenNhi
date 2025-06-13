console.log('Hello from QuanLyPhongJS');
console.log('File loaded at:', new Date().toISOString());

// API Configuration
const API_CONFIG = {
    // Use empty string for relative URLs (works with both localhost and ngrok)
    BASE_URL: '',
    // Alternative: Set specific URL when needed
    // BASE_URL: 'http://localhost:5157',
    // BASE_URL: 'https://your-ngrok-url.ngrok.io',
};

// Helper function to get full API URL
function getApiUrl(endpoint) {
    return API_CONFIG.BASE_URL + endpoint;
}

// Check if required libraries are loaded
function checkDependencies() {
    console.log('Checking dependencies...');
    const dependencies = {
        three: typeof THREE !== 'undefined',
        orbitControls: typeof THREE !== 'undefined' && typeof THREE.OrbitControls !== 'undefined',
        gsap: typeof gsap !== 'undefined',
        qrScanner: typeof Html5Qrcode !== 'undefined'
    };
    
    console.log('Dependencies status:', dependencies);
    
    if (!dependencies.three) {
        console.error('Three.js is not loaded');
        return false;
    }
    if (!dependencies.orbitControls) {
        console.error('OrbitControls is not loaded');
        return false;
    }
    if (!dependencies.gsap) {
        console.error('GSAP is not loaded');
        return false;
    }
    if (!dependencies.qrScanner) {
        console.warn('QR Scanner is not loaded - QR functionality will be disabled');
    }
    return true;
}

// Global variables
window.currentView = '2d';
window.scene = null;
window.camera = null;
window.renderer = null;
window.controls = null;
window.rooms3D = [];
window.modal = null;
let allLoaiPhong = []; // Store all room types
let allProducts = [];
let currentModal = null;
let modalElement = null;
let customOverlay = null;
let previousActiveElement = null;

// QR Scanner variables
let html5QrCode = null;
let currentStream = null;
let isFlashlightOn = false;

// Modal management system
const modalManager = {
    activeModals: [],
    zIndexBase: 1050,
    
    register(modalInstance, element) {
        this.activeModals.push({ instance: modalInstance, element: element });
        this.updateZIndexes();
    },
    
    unregister(modalInstance) {
        this.activeModals = this.activeModals.filter(m => m.instance !== modalInstance);
        this.updateZIndexes();
    },
    
    updateZIndexes() {
        this.activeModals.forEach((modal, index) => {
            if (modal.element) {
                modal.element.style.zIndex = this.zIndexBase + (index * 10);
            }
        });
    },
    
    closeAll() {
        // Close modals in reverse order (top to bottom)
        const modalsToClose = [...this.activeModals].reverse();
        modalsToClose.forEach(modal => {
            if (modal.instance && modal.instance.hide) {
                modal.instance.hide();
            }
        });
        this.activeModals = [];
    },
    
    getTopModal() {
        return this.activeModals[this.activeModals.length - 1];
    }
};

// Add missing functions
async function loadLoaiPhong() {
    try {
        const response = await fetch(getApiUrl('/api/getallloaiphong'));
        allLoaiPhong = await response.json();
        return allLoaiPhong;
    } catch (error) {
        console.error('Error loading room types:', error);
        allLoaiPhong = [];
        throw error;
    }
}

async function loadProducts() {
    try {
        const response = await fetch(getApiUrl('/api/getsanpham'));
        allProducts = await response.json();
        return allProducts;
    } catch (error) {
        console.error('Error loading products:', error);
        allProducts = [];
        throw error;
    }
}

function updatePriceInfo(loaiPhongId) {
    const priceInfoDiv = document.getElementById('priceInfo');
    if (!priceInfoDiv) return;

    const selectedLoaiPhong = allLoaiPhong.find(lp => lp.id === parseInt(loaiPhongId));
    if (selectedLoaiPhong) {
        priceInfoDiv.innerHTML = `
            <p class="mb-3"><strong><i class="fas fa-tags"></i> Bảng Giá:</strong></p>
            <div class="price-details">
                <p><i class="fas fa-clock"></i> Giờ đầu: ${formatCurrency(selectedLoaiPhong.gioDau)}đ</p>
                <p><i class="fas fa-clock"></i> Giờ sau: ${formatCurrency(selectedLoaiPhong.gioSau)}đ/giờ</p>
                <p><i class="fas fa-moon"></i> Qua đêm (23h - 8h): ${formatCurrency(selectedLoaiPhong.quaDem)}đ</p>
                <p class="mt-2"><i class="fas fa-info-circle"></i> ${selectedLoaiPhong.moTa || 'Không có mô tả'}</p>
            </div>
        `;
    } else {
        priceInfoDiv.innerHTML = '<p class="text-warning"><i class="fas fa-exclamation-triangle"></i> Không có thông tin giá phòng</p>';
    }
}

async function renderRooms() {
    try {
        const response = await fetch(getApiUrl("/api/getphong"));
        const rooms = await response.json();

        const topRow = document.getElementById('topRow');
        const bottomRow = document.getElementById('bottomRow');

        if (!topRow || !bottomRow) return;

        topRow.innerHTML = '';
        bottomRow.innerHTML = '';

        // Clear existing 3D rooms if in 3D view
        if (currentView === '3d' && scene) {
            rooms3D.forEach(room => scene.remove(room));
            rooms3D = [];

            // Arrange rooms in exactly 2 rows
            const roomsPerRow = Math.ceil(rooms.length / 2); // Chia đều cho 2 hàng
            const rowSpacing = 4; // Space between rows
            const colSpacing = 3; // Space between columns
            
            rooms.forEach((room, index) => {
                const row = Math.floor(index / roomsPerRow);
                const col = index % roomsPerRow;
                
                // Only create rooms for first 2 rows
                if (row < 2) {
                    // Create 3D room
                    const room3D = create3DRoom(room);
                    
                    // Position room in grid with better spacing
                    room3D.position.set(
                        (col - (roomsPerRow - 1) / 2) * colSpacing,
                        0,
                        (row * rowSpacing) - 2
                    );
                    
                    scene.add(room3D);
                    rooms3D.push(room3D);
                }
            });

            // Adjust camera position for better view of 2 rows
            camera.position.set(0, 12, 20);
            camera.lookAt(0, 0, 0);
            
            if (controls) {
                controls.update();
            }
        } else {
            // 2D view rendering - Arrange rooms in 2 rows
            const roomsPerRow = Math.ceil(rooms.length / 2); // Chia đều cho 2 hàng
            
            rooms.forEach((room, index) => {
                const div = document.createElement('div');
                div.classList.add('room', 'animate__animated', 'animate__fadeIn', 'room-3d');
                div.classList.add(room.trangThai === 1 ? 'red' : room.trangThai === 3 ? 'yellow' : 'green');
                
                const icon = document.createElement('i');
                icon.classList.add('fas', room.trangThai === 1 ? 'fa-bed' : room.trangThai === 3 ? 'fa-broom' : 'fa-door-open');
                div.appendChild(icon);
                
                const name = document.createElement('span');
                name.textContent = room.tenPhong;
                name.style.transform = 'translateZ(10px)';
                div.appendChild(name);
                
                const status = document.createElement('span');
                status.classList.add('status-badge');
                status.textContent = room.trangThai === 1 ? 'Đang thuê' : room.trangThai === 3 ? 'Đang dọn dẹp' : 'Trống';
                div.appendChild(status);

                div.dataset.id = room.id;
                div.style.animationDelay = `${index * 0.1}s`;

                div.addEventListener('click', () => {
                    gsap.to(div, {
                        scale: 0.95,
                        duration: 0.1,
                        yoyo: true,
                        repeat: 1,
                        onComplete: () => openModal(room)
                    });
                });

                // Xếp phòng thành 2 hàng: hàng đầu và hàng dưới
                if (index < roomsPerRow) {
                    topRow.appendChild(div);
                } else {
                    bottomRow.appendChild(div);
                }
            });
        }
    } catch (error) {
        console.error('Error rendering rooms:', error);
    }
}

function createCustomOverlay() {
    // Remove any existing overlay
    removeCustomOverlay();
    
    // Create new overlay
    customOverlay = document.createElement('div');
    customOverlay.className = 'custom-modal-overlay';
    document.body.appendChild(customOverlay);
    
    // Fade in
    setTimeout(() => customOverlay.classList.add('show'), 10);
    
    // Add click handler
    customOverlay.addEventListener('click', (e) => {
        if (e.target === customOverlay) {
            closeModal();
        }
    });
}

function removeCustomOverlay() {
    if (customOverlay) {
        customOverlay.classList.remove('show');
        setTimeout(() => {
            if (customOverlay && customOverlay.parentNode) {
                customOverlay.parentNode.removeChild(customOverlay);
            }
            customOverlay = null;
        }, 300);
    }
}

function initializeRoomModal() {
    try {
        // Don't reinitialize if already active
        if (currentModal && modalElement) {
            return true;
        }

        // Remove any existing backdrops before initializing
        document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
            backdrop.remove();
        });

        modalElement = document.getElementById('roomModal');
        if (!modalElement) {
            console.log('Modal element not found, creating it...');
            modalElement = document.createElement('div');
            modalElement.id = 'roomModal';
            modalElement.className = 'modal fade';
            modalElement.setAttribute('tabindex', '-1');
            modalElement.setAttribute('role', 'dialog');
            modalElement.setAttribute('aria-labelledby', 'roomModalLabel');
            modalElement.setAttribute('aria-hidden', 'true');
            modalElement.innerHTML = `
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title" id="roomModalLabel">Chi Tiết Phòng</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div id="modalContent" tabindex="-1"></div>
                        </div>
                    </div>
                </div>
            `;
            document.body.appendChild(modalElement);
        }

        // Initialize Bootstrap modal with custom backdrop handling
        currentModal = new bootstrap.Modal(modalElement, {
            backdrop: true,
            keyboard: true,
            focus: true
        });

        // Override the backdrop behavior
        const originalShow = currentModal.show;
        currentModal.show = function() {
            // Remove any existing backdrops first
            document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
                backdrop.remove();
            });
            originalShow.call(this);
        };

        // Set up event listeners
        modalElement.addEventListener('show.bs.modal', function(event) {
            previousActiveElement = document.activeElement;
            modalManager.register(currentModal, modalElement);
        });

        modalElement.addEventListener('shown.bs.modal', function() {
            // Ensure only one backdrop exists
            const backdrops = document.querySelectorAll('.modal-backdrop');
            if (backdrops.length > 1) {
                for (let i = 1; i < backdrops.length; i++) {
                    backdrops[i].remove();
                }
            }
            
            // Add null checks for focus management
            try {
                const firstInput = modalElement.querySelector('input:not([type="hidden"]), select, textarea, button:not([data-bs-dismiss="modal"])');
                if (firstInput && typeof firstInput.focus === 'function') {
                    setTimeout(() => {
                        firstInput.focus();
                    }, 100);
                } else {
                    const modalContent = modalElement.querySelector('.modal-content');
                    if (modalContent) {
                        modalContent.setAttribute('tabindex', '-1');
                        setTimeout(() => {
                            if (modalContent && typeof modalContent.focus === 'function') {
                                modalContent.focus();
                            }
                        }, 100);
                    }
                }
            } catch (error) {
                console.warn('Error setting focus:', error);
            }
        });

        modalElement.addEventListener('hide.bs.modal', function() {
            modalManager.unregister(currentModal);
        });

        modalElement.addEventListener('hidden.bs.modal', function() {
            // Remove all backdrops when modal is hidden
            document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
                backdrop.remove();
            });
            
            if (previousActiveElement) {
                previousActiveElement.focus();
            }
            previousActiveElement = null;
            
            // Clean up after modal is hidden
            currentModal = null;
            modalElement = null;
            
            // Reset body styles if no other modals are open
            if (modalManager.activeModals.length === 0) {
                document.body.classList.remove('modal-open');
                document.body.style.overflow = '';
                document.body.style.paddingRight = '';
            }
        });

        return true;
    } catch (error) {
        console.error('Error initializing room modal:', error);
        return false;
    }
}

function closeModal() {
    try {
        if (currentModal) {
            currentModal.hide();
        }
    } catch (error) {
        console.error('Error closing modal:', error);
        cleanupModal();
    }
}

function cleanupModal() {
    try {
        // Reset body styles
        document.body.classList.remove('modal-open');
        document.body.style.overflow = '';
        document.body.style.paddingRight = '';

        // Remove all backdrops
        document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
            if (backdrop && backdrop.parentNode) {
                backdrop.parentNode.removeChild(backdrop);
            }
        });

        // Close all modals through modal manager
        modalManager.closeAll();

        // Reset modal variables
        currentModal = null;
        modalElement = null;
        previousActiveElement = null;
        customOverlay = null;

    } catch (error) {
        console.error('Error cleaning up modal:', error);
        // Emergency cleanup
        document.body.classList.remove('modal-open');
        document.body.style.overflow = '';
        document.body.style.paddingRight = '';
        document.querySelectorAll('.modal-backdrop').forEach(b => b.remove());
        modalManager.activeModals = [];
    }
}

// Global function to clean all backdrops
function cleanAllBackdrops() {
    document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
        backdrop.remove();
    });
}

// Initialize when document is ready
document.addEventListener('DOMContentLoaded', function() {
    try {
        // Clean any stray backdrops from previous sessions
        cleanAllBackdrops();
        
        // Initialize modal
        if (!initializeRoomModal()) {
            console.error('Failed to initialize room modal');
            return;
        }

        // Add escape key handler
        document.addEventListener('keydown', function(event) {
            if (event.key === 'Escape') {
                closeModal();
            }
        });

        // Add click outside handler
        document.addEventListener('click', function(event) {
            if (modalElement && 
                currentModal && 
                !modalElement.contains(event.target) && 
                event.target.closest('.modal') === null) {
                closeModal();
            }
        });

        // Periodically clean orphaned backdrops
        setInterval(function() {
            if (modalManager.activeModals.length === 0) {
                const backdrops = document.querySelectorAll('.modal-backdrop');
                if (backdrops.length > 0) {
                    console.log('Cleaning orphaned backdrops...');
                    cleanAllBackdrops();
                    document.body.classList.remove('modal-open');
                    document.body.style.overflow = '';
                    document.body.style.paddingRight = '';
                }
            }
        }, 1000);

        // Initialize rooms
        renderRooms();

    } catch (error) {
        console.error('Error in DOMContentLoaded:', error);
    }
});

async function openModal(room) {
    try {
        // Initialize modal if needed
        if (!currentModal) {
            if (!initializeRoomModal()) {
                console.error('Failed to initialize modal');
                toastr.error('Không thể khởi tạo modal. Vui lòng thử lại sau.');
                return;
            }
        }

        // Load required data
        try {
            if (allLoaiPhong.length === 0) {
                await loadLoaiPhong();
            }
            if (allProducts.length === 0) {
                await loadProducts();
            }
        } catch (error) {
            console.error('Error loading required data:', error);
            toastr.error('Không thể tải dữ liệu cần thiết. Vui lòng thử lại sau.');
            return;
        }

        let modalHtml = '';
        if (room.trangThai === 1) {
            try {
                const response = await fetch(`/api/getthongtinthue/${room.id}`);
                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }
                const thongTinThue = await response.json();

                if (!thongTinThue) {
                    throw new Error('Không tìm thấy thông tin thuê phòng');
                }

                const thoiGianVao = new Date(thongTinThue.thoiGianVao);
                const thoiGianHienTai = new Date(thongTinThue.thoiGianHienTai);
                const thongTinGia = thongTinThue.thongTinGia;

                // Get purchased products with quantities
                const sanPhamDaMua = thongTinThue.sanPhamDaMua || {};
                const boughtProducts = allProducts.filter(p => sanPhamDaMua[p.id]);
                
                // Create HTML for purchased products
                const boughtProductsHtml = boughtProducts.length > 0 ? `
                    <div class="bought-products mb-4">
                        <h5><i class="fas fa-shopping-cart"></i> Sản phẩm đã mua:</h5>
                        <div class="product-list">
                            ${boughtProducts.map(p => `
                                <div class="product-item">
                                    <img src="/img/${p.hinhAnh}" class="product-thumbnail" onerror="this.src='/img/default.jpg'">
                                    <div class="product-details">
                                        <span class="product-name">${p.tenSanPham}</span>
                                        <span class="product-quantity">Số lượng: ${sanPhamDaMua[p.id]}</span>
                                        <span class="product-price">${formatCurrency(p.gia * sanPhamDaMua[p.id])}đ</span>
                                    </div>
                                </div>
                            `).join('')}
                        </div>
                    </div>
                ` : '';

                // Create HTML for available products
                const availableProductsHtml = `
                    <div class="available-products mb-4">
                        <button class="btn btn-primary mb-3" onclick="showPurchaseModal(${thongTinThue.id})">
                            <i class="fas fa-shopping-cart"></i> Mua Sản Phẩm
                        </button>
                    </div>
                `;

                modalHtml = `
                    <div class="room-info animate__animated animate__fadeIn">
                        <h4 class="mb-4"><i class="fas fa-info-circle"></i> Thông tin phòng ${room.tenPhong}</h4>
                        <p class="mb-3"><i class="fas fa-circle text-danger"></i> Trạng thái: <span class="badge bg-danger">Đang thuê</span></p>
                        
                        <form id="editForm" onsubmit="handleEditRoom(event, ${thongTinThue.id})" class="customer-info mb-4">
                            <div class="d-flex justify-content-between align-items-center mb-3">
                                <h5><i class="fas fa-user"></i> Thông tin khách hàng:</h5>
                                <button type="button" class="qr-scan-button" onclick="startQRScanner()">
                                    <i class="fas fa-qrcode"></i> Quét CCCD
                                </button>
                            </div>
                            <div class="form-group mb-3">
                                <label>Họ tên:</label>
                                <input type="text" name="hoTen" class="form-control" value="${thongTinThue.khachHang.hoTen || ''}" placeholder="Nhập họ tên">
                            </div>
                            <div class="form-group mb-3">
                                <label>CCCD:</label>
                                <input type="text" name="cccd" class="form-control" value="${thongTinThue.khachHang.cccd || ''}" placeholder="Nhập CCCD">
                            </div>
                            <div class="form-group mb-3">
                                <label>Giới tính:</label>
                                <select name="gioiTinh" class="form-control">
                                    <option value="">-- Chọn giới tính --</option>
                                    <option value="Nam" ${thongTinThue.khachHang.gioiTinh === 'Nam' ? 'selected' : ''}>Nam</option>
                                    <option value="Nữ" ${thongTinThue.khachHang.gioiTinh === 'Nữ' ? 'selected' : ''}>Nữ</option>
                                </select>
                            </div>
                            <div class="form-group mb-3">
                                <label>Ngày sinh:</label>
                                <input type="text" name="ngaySinh" class="form-control" value="${thongTinThue.khachHang.ngaySinh || ''}" placeholder="Chọn ngày sinh">
                            </div>
                            <div class="form-group mb-3">
                                <label>Loại phòng:</label>
                                <select name="loaiPhong" class="form-control" required>
                                    <option value="">-- Chọn loại phòng --</option>
                                    ${allLoaiPhong.map(lp => `
                                        <option value="${lp.id}" ${lp.id === thongTinThue.loaiPhong.id ? 'selected' : ''}>
                                            ${lp.tenLoai}
                                        </option>
                                    `).join('')}
                                </select>
                            </div>
                            <button type="submit" class="btn btn-primary mb-3">
                                <i class="fas fa-save"></i> Lưu thay đổi
                            </button>
                        </form>

                        <div class="rental-info mb-4">
                            <h5><i class="fas fa-clock"></i> Thông tin thuê phòng:</h5>
                            <p>Giờ vào: ${formatDateTime(thoiGianVao)}</p>
                            <p>Thời gian hiện tại: ${formatDateTime(thoiGianHienTai)}</p>
                            <p>Số giờ: ${thongTinGia.soGio} giờ</p>
                        </div>

                        ${boughtProductsHtml}
                        ${availableProductsHtml}

                        <div class="price-info">
                            <h5><i class="fas fa-money-bill-wave"></i> Thông tin giá:</h5>
                            ${thongTinGia.isQuaDem ? `
                                <p>Áp dụng giá qua đêm: ${formatCurrency(thongTinGia.giaQuaDem)}đ</p>
                                ${thongTinGia.giaGioSau > 0 ? `
                                    <p>Phụ thu giờ sau 8h: ${formatCurrency(thongTinGia.giaGioSau)}đ/giờ</p>
                                ` : ''}
                            ` : `
                                <p>Giờ đầu: ${formatCurrency(thongTinGia.giaGioDau)}đ</p>
                                <p>Giờ sau: ${formatCurrency(thongTinGia.giaGioSau)}đ/giờ</p>
                            `}
                            <p class="total-price mt-3">Tổng tiền hiện tại: ${formatCurrency(thongTinGia.tongTien)}đ</p>
                        </div>
                    </div>
                    <button onclick="handleCheckout(${thongTinThue.id})" class="btn btn-danger w-100">
                        <i class="fas fa-check-circle"></i> Thanh Toán
                    </button>
                `;

                // Add styles for the product list
                const style = document.createElement('style');
                style.textContent = `
                    .product-item {
                        display: flex;
                        align-items: center;
                        gap: 15px;
                        background: #f8f9fa;
                        padding: 12px;
                        border-radius: 10px;
                        margin-bottom: 10px;
                    }
                    .product-thumbnail {
                        width: 60px;
                        height: 60px;
                        object-fit: cover;
                        border-radius: 8px;
                    }
                    .product-details {
                        flex: 1;
                        display: flex;
                        flex-direction: column;
                    }
                    .product-name {
                        font-weight: 600;
                        margin-bottom: 5px;
                    }
                    .product-quantity {
                        color: #666;
                        font-size: 0.9em;
                    }
                    .product-price {
                        color: #e74c3c;
                        font-weight: 600;
                    }
                    .purchase-modal {
                        max-width: 800px;
                    }
                    .purchase-grid {
                        display: grid;
                        grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
                        gap: 20px;
                        margin-top: 20px;
                    }
                    .purchase-card {
                        background: #fff;
                        border-radius: 10px;
                        box-shadow: 0 2px 4px rgba(0,0,0,0.1);
                        overflow: hidden;
                        transition: transform 0.3s ease;
                    }
                    .purchase-card:hover {
                        transform: translateY(-5px);
                    }
                    .purchase-image {
                        width: 100%;
                        height: 150px;
                        object-fit: cover;
                    }
                    .purchase-info {
                        padding: 15px;
                    }
                    .quantity-control {
                        display: flex;
                        align-items: center;
                        gap: 10px;
                        margin-top: 10px;
                    }
                    .quantity-control button {
                        width: 30px;
                        height: 30px;
                        border-radius: 50%;
                        border: none;
                        background: #f0f0f0;
                        cursor: pointer;
                    }
                    .quantity-control input {
                        width: 50px;
                        text-align: center;
                        border: 1px solid #ddd;
                        border-radius: 5px;
                    }
                `;
                document.head.appendChild(style);
            } catch (error) {
                console.error('Error fetching rental info:', error);
                toastr.error('Không thể tải thông tin thuê phòng');
                return;
            }
        } else if (room.trangThai === 3) {
            modalHtml = `
                <div class="room-info animate__animated animate__fadeIn">
                    <h4 class="mb-4"><i class="fas fa-broom"></i> Thông tin phòng ${room.tenPhong}</h4>
                    <p class="mb-3"><i class="fas fa-circle text-warning"></i> Trạng thái: <span class="badge bg-warning">Đang dọn dẹp</span></p>
                    <button onclick="markRoomAsClean(${room.id})" class="btn btn-success w-100">
                        <i class="fas fa-check-circle"></i> Đã dọn dẹp xong
                    </button>
                </div>
            `;
        } else {
            const loaiPhongOptions = allLoaiPhong.map(lp => `
                <option value="${lp.id}" ${lp.id === room.idLoaiPhongMacDinh ? 'selected' : ''}>
                    ${lp.tenLoai}
                </option>
            `).join('');

            modalHtml = `
                <div class="room-info animate__animated animate__fadeIn">
                    <h4 class="mb-4"><i class="fas fa-info-circle"></i> Thông tin phòng ${room.tenPhong}</h4>
                    <p class="mb-3"><i class="fas fa-circle text-success"></i> Trạng thái: <span class="badge bg-success">Trống</span></p>
                    <div id="priceInfo" class="price-info mb-4">
                        <!-- Price info will be updated by updatePriceInfo -->
                    </div>
                </div>
                <form id="rentForm" onsubmit="handleRentRoom(event, ${room.id})" class="animate__animated animate__fadeIn">
                    <div class="form-group">
                        <label><i class="fas fa-bed"></i> Loại phòng:</label>
                        <select name="loaiPhong" class="form-control" required onchange="updatePriceInfo(this.value)">
                            <option value="">-- Chọn loại phòng --</option>
                            ${loaiPhongOptions}
                        </select>
                    </div>
                    
                    <!-- QR Scan Section -->
                    <div class="qr-scan-section mb-4">
                        <div class="d-flex justify-content-between align-items-center mb-3">
                            <h6 class="mb-0"><i class="fas fa-user-friends"></i> Thông tin khách hàng:</h6>
                            <button type="button" class="qr-scan-button" onclick="startQRScanner()">
                                <i class="fas fa-qrcode"></i> Quét CCCD
                            </button>
                        </div>
                        <p class="text-muted small mb-3">
                            <i class="fas fa-info-circle"></i> Có thể quét QR code trên CCCD để tự động điền thông tin
                        </p>
                    </div>
                    
                    <div class="form-group">
                        <label><i class="fas fa-user"></i> Họ tên khách hàng:</label>
                        <input type="text" name="hoTen" class="form-control" placeholder="Nhập họ tên (không bắt buộc)">
                    </div>
                    <div class="form-group">
                        <label><i class="fas fa-id-card"></i> CCCD:</label>
                        <input type="text" name="cccd" class="form-control" placeholder="Nhập CCCD (không bắt buộc)">
                    </div>
                    <div class="form-group">
                        <label><i class="fas fa-venus-mars"></i> Giới tính:</label>
                        <select name="gioiTinh" class="form-control">
                            <option value="">-- Chọn giới tính (không bắt buộc) --</option>
                            <option value="Nam">Nam</option>
                            <option value="Nữ">Nữ</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label><i class="fas fa-calendar"></i> Ngày sinh:</label>
                        <input type="text" name="ngaySinh" class="form-control" placeholder="Chọn ngày sinh (không bắt buộc)" autocomplete="off">
                    </div>
                    <button type="submit" class="btn btn-primary w-100 mt-4">
                        <i class="fas fa-key"></i> Thuê Phòng
                    </button>
                </form>
            `;
        }

        // Update modal content
        const modalContent = document.querySelector('#modalContent');
        if (modalContent) {
            modalContent.innerHTML = modalHtml;
        }

        // Show modal
        if (currentModal) {
            currentModal.show();
            document.body.classList.add('modal-open');
            document.body.style.overflow = 'hidden';
        }

        // Update price info if needed
        if (!room.trangThai && room.idLoaiPhongMacDinh) {
            updatePriceInfo(room.idLoaiPhongMacDinh);
        }

    } catch (error) {
        console.error('Error opening modal:', error);
        toastr.error('Có lỗi xảy ra khi mở modal');
        cleanupModal();
    }
}

function formatCurrency(value) {
    if (!value) return '0';
    return new Intl.NumberFormat('vi-VN').format(value);
}

function formatDateTime(date) {
    const d = new Date(date);
    return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')} ${d.getDate()}/${d.getMonth() + 1}/${d.getFullYear()}`;
}

async function handleRentRoom(event, idPhong) {
    event.preventDefault();
    const loading = showLoading();
    
    const form = event.target;
    
    const idLoaiPhong = parseInt(form.loaiPhong.value);
    if (!idLoaiPhong) {
        toastr.warning('Vui lòng chọn loại phòng');
        hideLoading(loading);
        return;
    }

    const formData = {
        idPhong: idPhong,
        idLoaiPhong: idLoaiPhong,
        hoTen: form.hoTen.value,
        cccd: form.cccd.value,
        gioiTinh: form.gioiTinh.value,
        ngaySinh: form.ngaySinh.value
    };

    try {
        const response = await fetch('/api/thuephong', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        });

        const result = await response.json();
        if (result.success) {
            closeModal();
            toastr.success('Thuê phòng thành công!');
            await renderRooms();
        } else {
            toastr.error('Lỗi: ' + result.message);
        }
    } catch (error) {
        toastr.error('Có lỗi xảy ra: ' + error.message);
    } finally {
        hideLoading(loading);
    }
}

async function handleCheckout(idPhong) {
    if (!confirm('Bạn có chắc muốn thanh toán phòng này?')) {
        return;
    }

    try {
        // Get product information from current modal
        let tongTienSanPham = 0;
        const modalContent = document.querySelector('#modalContent');
        if (modalContent) {
            const productItems = modalContent.querySelectorAll('.product-item');
            productItems.forEach(item => {
                const priceText = item.querySelector('.product-price').textContent;
                const price = parseInt(priceText.replace(/[^\d]/g, ''));
                if (!isNaN(price)) {
                    tongTienSanPham += price;
                }
            });
        }
        
        // Process checkout
        const response = await fetch('/api/traphong', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ 
                thuePhongId: idPhong,
                tongTienSanPham: tongTienSanPham
            })
        });

        const result = await response.json();
        if (result.success) {
            // Close all modals using modal manager
            modalManager.closeAll();
            
            // Clean up any remaining modal elements
            cleanupModal();
            
            // Calculate total amount
            const tongTienPhong = result.giaTien || 0;
            const tongCong = tongTienPhong + tongTienSanPham;
            
            // Show detailed toast
            toastr.success(`
                <div class="checkout-summary">
                    <h4><i class="fas fa-check-circle"></i> Thanh toán thành công!</h4>
                    <div class="summary-details">
                        <p><i class="fas fa-clock"></i> Tổng thời gian: ${result.thoiGianThue} giờ</p>
                        <p><i class="fas fa-bed"></i> Tiền phòng: ${formatCurrency(tongTienPhong)}đ</p>
                        <p><i class="fas fa-shopping-cart"></i> Tiền sản phẩm: ${formatCurrency(tongTienSanPham)}đ</p>
                        <p class="total"><i class="fas fa-money-bill-wave"></i> Tổng cộng: ${formatCurrency(tongCong)}đ</p>
                    </div>
                </div>
            `, '', {
                timeOut: 10000,
                extendedTimeOut: 5000,
                closeButton: true,
                progressBar: true,
                enableHtml: true
            });

            // Add styles for the checkout summary toast (if not already added)
            if (!document.querySelector('#checkoutToastStyles')) {
                const style = document.createElement('style');
                style.id = 'checkoutToastStyles';
                style.textContent = `
                    .checkout-summary {
                        padding: 10px 0;
                    }
                    .checkout-summary h4 {
                        color: #2ecc71;
                        margin-bottom: 15px;
                        font-size: 1.1em;
                    }
                    .summary-details {
                        background: rgba(255, 255, 255, 0.1);
                        padding: 10px;
                        border-radius: 8px;
                    }
                    .summary-details p {
                        margin: 8px 0;
                        font-size: 0.9em;
                    }
                    .summary-details .total {
                        margin-top: 15px;
                        padding-top: 10px;
                        border-top: 1px solid rgba(255, 255, 255, 0.2);
                        font-weight: bold;
                        color: #f1c40f;
                    }
                    .summary-details i {
                        margin-right: 8px;
                        width: 20px;
                        text-align: center;
                    }
                `;
                document.head.appendChild(style);
            }
            
            await renderRooms();
        } else {
            toastr.error('Lỗi: ' + result.message);
        }
    } catch (error) {
        console.error('Error during checkout:', error);
        toastr.error('Có lỗi xảy ra: ' + error.message);
    }
}

async function handleEditRoom(event, thuePhongId) {
    event.preventDefault();
    const form = event.target;
    
    const formData = {
        thuePhongId: thuePhongId,
        idLoaiPhong: parseInt(form.loaiPhong.value) || null,
        hoTen: form.hoTen.value,
        cccd: form.cccd.value,
        gioiTinh: form.gioiTinh.value,
        ngaySinh: form.ngaySinh.value
    };

    try {
        const response = await fetch('/api/capnhatthongtinthue', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(formData)
        });

        const result = await response.json();
        if (result.success) {
            toastr.success('Cập nhật thông tin thành công!');
            await renderRooms();
        } else {
            toastr.error('Lỗi: ' + result.message);
        }
    } catch (error) {
        toastr.error('Có lỗi xảy ra: ' + error.message);
    }
}

async function showPurchaseModal(thuePhongId) {
    try {
        // Store reference to the main room modal
        const roomModalElement = document.getElementById('roomModal');
        const roomModalInstance = currentModal;
        
        // Remove any existing purchase modal
        const existingPurchaseModal = document.querySelector('#purchaseModal');
        if (existingPurchaseModal) {
            const modalInstance = bootstrap.Modal.getInstance(existingPurchaseModal);
            if (modalInstance) {
                modalInstance.dispose();
            }
            existingPurchaseModal.remove();
        }

        // Count existing backdrops to manage them properly
        const existingBackdrops = document.querySelectorAll('.modal-backdrop').length;

        // Create purchase modal element
        const purchaseModalElement = document.createElement('div');
        purchaseModalElement.id = 'purchaseModal';
        purchaseModalElement.className = 'modal fade';
        purchaseModalElement.setAttribute('tabindex', '-1');
        purchaseModalElement.setAttribute('role', 'dialog');
        purchaseModalElement.setAttribute('aria-hidden', 'true');
        purchaseModalElement.setAttribute('data-bs-backdrop', 'false');
        purchaseModalElement.setAttribute('data-bs-keyboard', 'true');
        purchaseModalElement.innerHTML = `
            <div class="modal-dialog modal-dialog-centered modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Mua Sản Phẩm</h5>
                        <button type="button" class="btn-close" onclick="closePurchaseModal()" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <div class="purchase-grid">
                            ${allProducts.map(p => `
                                <div class="purchase-card">
                                    <img src="/img/${p.hinhAnh}" class="purchase-image" onerror="this.src='/img/default.jpg'">
                                    <div class="purchase-info">
                                        <div class="product-name">${p.tenSanPham}</div>
                                        <div class="product-price">${formatCurrency(p.gia)}đ</div>
                                        <div class="quantity-control">
                                            <button type="button" onclick="updateQuantity(${p.id}, -1)">-</button>
                                            <input type="number" id="quantity-${p.id}" value="1" min="1" max="99">
                                            <button type="button" onclick="updateQuantity(${p.id}, 1)">+</button>
                                            <button type="button" class="btn btn-primary btn-sm" onclick="purchaseProduct(${thuePhongId}, ${p.id})">
                                                <i class="fas fa-cart-plus"></i> Mua
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            `).join('')}
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Add modal to body
        document.body.appendChild(purchaseModalElement);

        // Initialize Bootstrap modal without creating new backdrop
        const purchaseModal = new bootstrap.Modal(purchaseModalElement, {
            backdrop: false,
            keyboard: true
        });

        // Override the show method to prevent backdrop creation
        const originalShow = purchaseModal.show;
        purchaseModal.show = function() {
            originalShow.call(this);
            // Manage backdrop visibility for stacked modals
            const allBackdrops = document.querySelectorAll('.modal-backdrop');
            if (allBackdrops.length > existingBackdrops) {
                // Remove extra backdrops
                for (let i = existingBackdrops; i < allBackdrops.length; i++) {
                    allBackdrops[i].remove();
                }
            }
        };

        // Set up event listeners
        purchaseModalElement.addEventListener('show.bs.modal', function() {
            modalManager.register(purchaseModal, purchaseModalElement);
        });

        purchaseModalElement.addEventListener('hide.bs.modal', function() {
            modalManager.unregister(purchaseModal);
        });

        purchaseModalElement.addEventListener('hidden.bs.modal', function() {
            // Remove the modal element after it's hidden
            if (purchaseModalElement.parentNode) {
                purchaseModalElement.parentNode.removeChild(purchaseModalElement);
            }
            
            // Reset body styles if no other modals are open
            if (modalManager.activeModals.length === 0) {
                document.body.classList.remove('modal-open');
                document.body.style.overflow = '';
                document.body.style.paddingRight = '';
                // Remove all backdrops
                document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
                    backdrop.remove();
                });
            }
        });

        // Store the purchase modal globally for the close function
        window.currentPurchaseModal = purchaseModal;

        // Add escape key handler for purchase modal
        const escapeHandler = function(event) {
            if (event.key === 'Escape' && document.getElementById('purchaseModal')) {
                closePurchaseModal();
            }
        };
        document.addEventListener('keydown', escapeHandler);

        // Add click outside handler
        const clickHandler = function(event) {
            const purchaseModalElement = document.getElementById('purchaseModal');
            if (purchaseModalElement && 
                purchaseModalElement.classList.contains('show') &&
                !purchaseModalElement.contains(event.target) &&
                event.target.closest('.modal') === null) {
                closePurchaseModal();
            }
        };
        document.addEventListener('click', clickHandler);

        // Clean up event handlers when modal is hidden
        purchaseModalElement.addEventListener('hidden.bs.modal', function onHidden() {
            document.removeEventListener('keydown', escapeHandler);
            document.removeEventListener('click', clickHandler);
            purchaseModalElement.removeEventListener('hidden.bs.modal', onHidden);
        });

        // Show the purchase modal
        purchaseModal.show();

    } catch (error) {
        console.error('Error showing purchase modal:', error);
        toastr.error('Có lỗi xảy ra khi mở modal mua sản phẩm');
    }
}

function closePurchaseModal() {
    try {
        // First hide the modal if it exists
        if (window.currentPurchaseModal) {
            window.currentPurchaseModal.hide();
        }
        
        // Remove the modal element
        const purchaseModalElement = document.getElementById('purchaseModal');
        if (purchaseModalElement) {
            // Remove event listeners
            const newElement = purchaseModalElement.cloneNode(true);
            purchaseModalElement.parentNode.replaceChild(newElement, purchaseModalElement);
            
            // Remove the element
            setTimeout(() => {
                const modal = document.getElementById('purchaseModal');
                if (modal) {
                    modal.remove();
                }
            }, 300);
        }
        
        // Clean up backdrop
        cleanAllBackdrops();
        
        // Reset the global reference
        window.currentPurchaseModal = null;
        
        // Reset body styles if no other modals are open
        if (modalManager.activeModals.length <= 1) {
            setTimeout(() => {
                document.body.classList.remove('modal-open');
                document.body.style.overflow = '';
                document.body.style.paddingRight = '';
            }, 300);
        }
    } catch (error) {
        console.error('Error closing purchase modal:', error);
        // Force cleanup
        const modal = document.getElementById('purchaseModal');
        if (modal) {
            modal.remove();
        }
        window.currentPurchaseModal = null;
        cleanAllBackdrops();
    }
}

function updateQuantity(productId, change) {
    const input = document.getElementById(`quantity-${productId}`);
    if (input) {
        let value = parseInt(input.value) + change;
        value = Math.max(1, Math.min(99, value));
        input.value = value;
    }
}

async function purchaseProduct(thuePhongId, sanPhamId) {
    const quantity = parseInt(document.getElementById(`quantity-${sanPhamId}`).value);
    try {
        const response = await fetch('/api/muasanpham', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                thuePhongId: thuePhongId,
                sanPhamId: sanPhamId,
                soLuong: quantity
            })
        });

        const result = await response.json();
        if (result.success) {
            toastr.success('Mua sản phẩm thành công!');

            // Get current room info before closing modals
            const roomResponse = await fetch("/api/getphong");
            if (!roomResponse.ok) {
                throw new Error('Không thể tải thông tin phòng');
            }
            const rooms = await roomResponse.json();
            
            // Find current room from modal content
            let currentRoom = null;
            const modalContent = document.querySelector('#modalContent');
            if (modalContent) {
                const roomIdMatch = modalContent.innerHTML.match(/phòng\s+(\w+)/i);
                if (roomIdMatch) {
                    const roomName = roomIdMatch[1];
                    currentRoom = rooms.find(r => r.tenPhong === roomName && r.trangThai === 1);
                }
            }

            // First close the purchase modal
            if (window.currentPurchaseModal) {
                try {
                    window.currentPurchaseModal.hide();
                    
                    // Wait for modal to be fully hidden
                    await new Promise(resolve => {
                        const purchaseModalElement = document.getElementById('purchaseModal');
                        if (purchaseModalElement) {
                            purchaseModalElement.addEventListener('hidden.bs.modal', function onHidden() {
                                purchaseModalElement.removeEventListener('hidden.bs.modal', onHidden);
                                resolve();
                            });
                        } else {
                            resolve();
                        }
                        
                        // Timeout fallback
                        setTimeout(resolve, 500);
                    });
                } catch (error) {
                    console.error('Error closing purchase modal:', error);
                }
                window.currentPurchaseModal = null;
            }

            // Clean up purchase modal completely
            const purchaseModalElement = document.getElementById('purchaseModal');
            if (purchaseModalElement) {
                purchaseModalElement.remove();
            }

            // Clean up any stray backdrops
            cleanAllBackdrops();

            // Close the room modal
            if (currentModal) {
                currentModal.hide();
            }

            // Reset modal states
            modalManager.activeModals = [];
            currentModal = null;
            modalElement = null;

            // Wait a bit then reopen the room modal with updated info
            if (currentRoom) {
                setTimeout(() => {
                    // Make sure body styles are reset
                    document.body.classList.remove('modal-open');
                    document.body.style.overflow = '';
                    document.body.style.paddingRight = '';
                    
                    // Reopen modal with updated info
                    openModal(currentRoom);
                }, 500);
            }
        } else {
            toastr.error('Lỗi: ' + result.message);
        }
    } catch (error) {
        console.error('Error purchasing product:', error);
        toastr.error('Có lỗi xảy ra: ' + error.message);
    }
}

async function getCurrentRoom() {
    try {
        const response = await fetch("/api/getphong");
        const rooms = await response.json();
        const roomId = document.querySelector('.modal-body [data-room-id]')?.dataset.roomId;
        return rooms.find(r => r.id === parseInt(roomId));
    } catch (error) {
        console.error('Error getting current room:', error);
        return null;
    }
}

// Add loading animation
function showLoading() {
    console.log('Showing loading state');
    const loadingOverlay = document.querySelector('.loading-overlay');
    if (loadingOverlay) {
        loadingOverlay.style.display = 'flex';
        gsap.to(loadingOverlay, {
            opacity: 1,
            duration: 0.3
        });
    }
}

function hideLoading(loading) {
    console.log('Hiding loading state');
    const loadingOverlay = document.querySelector('.loading-overlay');
    if (loadingOverlay) {
        gsap.to(loadingOverlay, {
            opacity: 0,
            duration: 0.3,
            onComplete: () => {
                loadingOverlay.style.display = 'none';
            }
        });
    }
}

function toggleView(view) {
    console.log('Toggling view to:', view);
    console.log('Current view:', currentView);
    
    const roomScene = document.getElementById('roomScene');
    const roomsContainer = document.querySelector('.rooms-container');
    
    if (!roomScene || !roomsContainer) {
        console.error('Required DOM elements not found');
        return;
    }

    if (view === currentView) {
        console.log('Already in', view, 'view');
        return;
    }

    showLoading();
    
    if (view === '3d') {
        console.log('Switching to 3D view');
        
        // First hide 2D view
        gsap.to(roomsContainer, {
            opacity: 0,
            duration: 0.3,
            onComplete: () => {
                roomsContainer.style.display = 'none';
                
                // Then initialize or show 3D view
                if (!scene) {
                    console.log('Initializing 3D scene for the first time');
                    const success = init3DScene();
                    if (!success) {
                        console.error('Failed to initialize 3D scene');
                        hideLoading();
                        return;
                    }
                }
                
                roomScene.style.display = 'block';
                
                // Clear existing rooms and re-render
                if (scene) {
                    rooms3D.forEach(room => {
                        if (room) scene.remove(room);
                    });
                    rooms3D = [];
                }
                
                // Show 3D view and render rooms
                gsap.to(roomScene, {
                    opacity: 1,
                    duration: 0.3,
                    onComplete: () => {
                        console.log('3D view transition complete');
                        renderRooms();
                        hideLoading();
                        
                        // Force resize to ensure proper dimensions
                        window.dispatchEvent(new Event('resize'));
                    }
                });
            }
        });
    } else {
        console.log('Switching to 2D view');
        
        // First hide 3D view
        gsap.to(roomScene, {
            opacity: 0,
            duration: 0.3,
            onComplete: () => {
                roomScene.style.display = 'none';
                
                // Then show 2D view
                roomsContainer.style.display = 'block';
                gsap.to(roomsContainer, {
                    opacity: 1,
                    duration: 0.3,
                    onComplete: () => {
                        console.log('2D view transition complete');
                        renderRooms();
                        hideLoading();
                    }
                });
            }
        });
    }
    
    currentView = view;
    console.log('View updated to:', currentView);
}

function init3DScene() {
    console.log('Initializing 3D scene...');
    
    if (!checkDependencies()) {
        console.error('Failed to initialize 3D scene - dependencies not loaded');
        return false;
    }

    const container = document.getElementById('roomScene');
    if (!container) {
        console.error('Room scene container not found');
        return false;
    }

    // Make sure container is visible and has dimensions
    container.style.display = 'block';
    container.style.opacity = '1';
    
    console.log('Container dimensions:', {
        width: container.clientWidth,
        height: container.clientHeight,
        display: container.style.display,
        opacity: container.style.opacity
    });

    try {
        // Initialize scene with background
        scene = new THREE.Scene();
        scene.background = new THREE.Color(0xf0f0f0);
        console.log('Scene created successfully');

        // Initialize camera with better positioning
        camera = new THREE.PerspectiveCamera(
            75,
            container.clientWidth / container.clientHeight,
            0.1,
            1000
        );
        camera.position.set(0, 5, 10);
        camera.lookAt(0, 0, 0);
        console.log('Camera initialized:', {
            aspect: camera.aspect,
            position: camera.position
        });

        // Initialize renderer with proper settings
        renderer = new THREE.WebGLRenderer({ 
            antialias: true,
            alpha: true
        });
        renderer.setSize(container.clientWidth, container.clientHeight);
        renderer.setPixelRatio(window.devicePixelRatio);
        renderer.shadowMap.enabled = true;
        
        // Clear container before adding renderer
        container.innerHTML = '';
        container.appendChild(renderer.domElement);
        console.log('Renderer initialized and added to container');

        // Initialize controls with better settings
        controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.screenSpacePanning = false;
        controls.minDistance = 5;
        controls.maxDistance = 15;
        controls.maxPolarAngle = Math.PI / 2;
        console.log('OrbitControls initialized');

        // Add lights
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.7);
        scene.add(ambientLight);

        const directionalLight = new THREE.DirectionalLight(0xffffff, 1);
        directionalLight.position.set(5, 5, 5);
        directionalLight.castShadow = true;
        scene.add(directionalLight);
        console.log('Lights added to scene');

        // Add grid helper for better orientation
        const gridHelper = new THREE.GridHelper(20, 20, 0x888888, 0xcccccc);
        scene.add(gridHelper);
        console.log('Grid helper added');

        // Add click event handler for 3D rooms
        renderer.domElement.addEventListener('click', handle3DRoomClick);
        
        // Add hover effect handler
        renderer.domElement.addEventListener('mousemove', handle3DRoomHover);
        
        // Update cursor style
        renderer.domElement.style.cursor = 'pointer';

        // Start animation loop
        animate();
        console.log('Animation loop started');
        
        // Force a resize event to ensure proper dimensions
        window.dispatchEvent(new Event('resize'));
        
        return true;
    } catch (error) {
        console.error('Error initializing 3D scene:', error);
        return false;
    }
}

function create3DRoom(room) {
    // Create room group to hold all parts
    const roomGroup = new THREE.Group();
    
    // Create room geometry
    const geometry = new THREE.BoxGeometry(2, 1.5, 2);
    
    // Create materials with better visual effects
    const material = new THREE.MeshPhongMaterial({
        color: room.trangThai === 1 ? 0xff6b6b : room.trangThai === 3 ? 0xffd43b : 0x51cf66,
        shininess: 100,
        specular: 0x444444,
        flatShading: false
    });

    // Create room mesh
    const roomMesh = new THREE.Mesh(geometry, material);
    roomGroup.add(roomMesh);
    
    // Add roof
    const roofGeometry = new THREE.ConeGeometry(1.8, 0.8, 4);
    const roofMaterial = new THREE.MeshPhongMaterial({
        color: room.trangThai === 1 ? 0xff6b6b : room.trangThai === 3 ? 0xffd43b : 0x51cf66,
        shininess: 50
    });
    const roof = new THREE.Mesh(roofGeometry, roofMaterial);
    roof.position.y = 1.15;
    roof.rotation.y = Math.PI / 4;
    roomGroup.add(roof);

    // Add door
    const doorGeometry = new THREE.PlaneGeometry(0.6, 0.9);
    const doorMaterial = new THREE.MeshPhongMaterial({
        color: 0x654321,
        side: THREE.DoubleSide
    });
    const door = new THREE.Mesh(doorGeometry, doorMaterial);
    door.position.set(0, -0.3, 1.01);
    roomGroup.add(door);

    // Add door handle
    const handleGeometry = new THREE.SphereGeometry(0.05, 8, 8);
    const handleMaterial = new THREE.MeshPhongMaterial({ color: 0xffd700 });
    const handle = new THREE.Mesh(handleGeometry, handleMaterial);
    handle.position.set(0.2, -0.3, 1.05);
    roomGroup.add(handle);

    // Add room number on the roof using canvas texture
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    canvas.width = 64;
    canvas.height = 64;
    
    // Make background transparent - no fillRect needed
    
    // Add room number only
    context.fillStyle = '#333333';
    context.font = 'Bold 24px Arial';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    
    // Extract only the number from room name (remove "Phòng Số" if present)
    let roomNumber = room.tenPhong;
    if (roomNumber.includes('Phòng Số')) {
        roomNumber = roomNumber.replace('Phòng Số', '').trim();
    } else if (roomNumber.includes('Phòng')) {
        roomNumber = roomNumber.replace('Phòng', '').trim();
    }
    
    context.fillText(roomNumber, 32, 32);

    const texture = new THREE.CanvasTexture(canvas);
    const spriteMaterial = new THREE.SpriteMaterial({ 
        map: texture,
        sizeAttenuation: false
    });
    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.scale.set(0.25, 0.25, 0.25);
    sprite.position.set(0, 2.2, 0);
    roomGroup.add(sprite);

    // Add status icon
    const iconGeometry = new THREE.PlaneGeometry(0.5, 0.5);
    const iconCanvas = document.createElement('canvas');
    const iconContext = iconCanvas.getContext('2d');
    iconCanvas.width = 128;
    iconCanvas.height = 128;
    
    iconContext.fillStyle = room.trangThai === 1 ? '#ff6b6b' : room.trangThai === 3 ? '#ffd43b' : '#51cf66';
    iconContext.fillRect(0, 0, 128, 128);
    iconContext.fillStyle = 'white';
    iconContext.font = 'Bold 80px FontAwesome';
    iconContext.textAlign = 'center';
    iconContext.textBaseline = 'middle';
    iconContext.fillText(room.trangThai === 1 ? '🛏️' : room.trangThai === 3 ? '🧹' : '🚪', 64, 64);
    
    const iconTexture = new THREE.CanvasTexture(iconCanvas);
    const iconMaterial = new THREE.MeshBasicMaterial({ 
        map: iconTexture, 
        transparent: true,
        side: THREE.DoubleSide
    });
    const iconMesh = new THREE.Mesh(iconGeometry, iconMaterial);
    iconMesh.position.set(0, 0.3, 1.02);
    roomGroup.add(iconMesh);

    // Store room data and click handler
    roomGroup.userData = {
        roomData: room,
        onClick: () => {
            if (room) {
                openModal(room);
            }
        }
    };

    // Add hover effect
    roomGroup.traverse((child) => {
        if (child instanceof THREE.Mesh) {
            child.userData.originalColor = child.material.color.getHex();
        }
    });

    return roomGroup;
}

function animate() {
    if (!renderer || !scene || !camera) {
        console.warn('Missing required 3D components for animation');
        return;
    }

    requestAnimationFrame(animate);
    
    if (controls) {
        controls.update();
    }
    
    if (rooms3D && rooms3D.length > 0) {
        rooms3D.forEach((room, index) => {
            if (room) {
                // Subtle floating animation
                room.position.y = Math.sin(Date.now() * 0.001 + index * 0.5) * 0.1;
                
                // Find the sprite (room number) and rotate it to face camera
                room.traverse((child) => {
                    if (child instanceof THREE.Sprite) {
                        child.lookAt(camera.position);
                    }
                });
            }
        });
    }

    try {
        renderer.render(scene, camera);
    } catch (error) {
        console.error('Error rendering scene:', error);
    }
}

// Add resize handler
window.addEventListener('resize', function() {
    if (renderer && camera) {
        const container = document.getElementById('roomScene');
        if (container) {
            const width = container.clientWidth;
            const height = container.clientHeight;
            
            camera.aspect = width / height;
            camera.updateProjectionMatrix();
            
            renderer.setSize(width, height);
            renderer.setPixelRatio(window.devicePixelRatio);
            
            console.log('Resized renderer:', { width, height });
        }
    }
});

// Add hover effect handler for 3D rooms
let hoveredRoom = null;

function handle3DRoomHover(event) {
    if (!renderer || !camera || !scene) return;

    const raycaster = new THREE.Raycaster();
    const mouse = new THREE.Vector2();

    // Calculate mouse position in normalized device coordinates
    const rect = renderer.domElement.getBoundingClientRect();
    mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    raycaster.setFromCamera(mouse, camera);

    // Check for intersections with 3D rooms
    const intersects = raycaster.intersectObjects(rooms3D, true);

    if (intersects.length > 0) {
        // Find the root room object
        let roomObject = intersects[0].object;
        while (roomObject.parent && roomObject.parent !== scene) {
            roomObject = roomObject.parent;
        }

        // Apply hover effect
        if (hoveredRoom !== roomObject) {
            // Remove previous hover effect
            if (hoveredRoom) {
                hoveredRoom.traverse((child) => {
                    if (child instanceof THREE.Mesh && child.userData.originalColor) {
                        child.material.color.setHex(child.userData.originalColor);
                    }
                });
                hoveredRoom.scale.set(1, 1, 1);
            }

            // Apply new hover effect
            hoveredRoom = roomObject;
            roomObject.traverse((child) => {
                if (child instanceof THREE.Mesh) {
                    const newColor = new THREE.Color(child.material.color);
                    newColor.multiplyScalar(1.2);
                    child.material.color = newColor;
                }
            });
            roomObject.scale.set(1.1, 1.1, 1.1);
        }
        renderer.domElement.style.cursor = 'pointer';
    } else {
        // Remove hover effect when not hovering any room
        if (hoveredRoom) {
            hoveredRoom.traverse((child) => {
                if (child instanceof THREE.Mesh && child.userData.originalColor) {
                    child.material.color.setHex(child.userData.originalColor);
                }
            });
            hoveredRoom.scale.set(1, 1, 1);
            hoveredRoom = null;
        }
        renderer.domElement.style.cursor = 'default';
    }
}

// Add this new function to handle 3D room clicks
function handle3DRoomClick(event) {
    event.preventDefault();
    if (!renderer || !camera || !scene) return;

    const raycaster = new THREE.Raycaster();
    const mouse = new THREE.Vector2();

    // Calculate mouse position in normalized device coordinates
    const rect = renderer.domElement.getBoundingClientRect();
    mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    raycaster.setFromCamera(mouse, camera);

    // Check for intersections with 3D rooms
    const intersects = raycaster.intersectObjects(rooms3D, true);

    if (intersects.length > 0) {
        // Find the root room object
        let roomObject = intersects[0].object;
        while (roomObject.parent && roomObject.parent !== scene) {
            roomObject = roomObject.parent;
        }

        if (roomObject.userData && roomObject.userData.onClick) {
            roomObject.userData.onClick();
        }
    }
}

async function markRoomAsClean(roomId) {
    try {
        const response = await fetch('/api/dondepphong', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ roomId: roomId })
        });

        const result = await response.json();
        if (result.success) {
            toastr.success('Phòng đã được đánh dấu là đã dọn dẹp!');
            await renderRooms(); // Refresh the room display
        } else {
            toastr.error('Lỗi: ' + result.message);
        }
    } catch (error) {
        console.error('Error marking room as clean:', error);
        toastr.error('Có lỗi xảy ra khi đánh dấu phòng đã dọn dẹp');
    }
}

// QR Code Scanning Functions
async function startQRScanner() {
    try {
        // Show QR scanner modal
        const qrModal = new bootstrap.Modal(document.getElementById('qrScannerModal'));
        qrModal.show();
        
        const statusElement = document.getElementById('scannerStatus');
        
        // Update status
        statusElement.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Đang khởi động camera...';
        
        // Check if Html5Qrcode is available
        if (typeof Html5Qrcode === 'undefined') {
            throw new Error('QR Scanner library không được tải');
        }

        // Initialize Html5Qrcode
        html5QrCode = new Html5Qrcode("qrScannerVideo");
        
        // Get available cameras
        const devices = await Html5Qrcode.getCameras();
        if (devices && devices.length > 0) {
            // Use the back camera if available, otherwise use the first camera
            const cameraId = devices.find(device => 
                device.label.toLowerCase().includes('back') || 
                device.label.toLowerCase().includes('rear')
            )?.id || devices[0].id;

            // Start scanning
            await html5QrCode.start(
                cameraId,
                {
                    fps: 10,    // Optional, frame per seconds for qr code scanning
                    qrbox: { width: 250, height: 250 }  // Optional, if you want bounded box UI
                },
                (decodedText, decodedResult) => {
                    // Handle successful scan
                    handleQRResult({ data: decodedText });
                },
                (errorMessage) => {
                    // Handle scan error - this is called continuously, so we ignore it
                    // console.log(`Code scan error = ${errorMessage}`);
                }
            );
            
            // Update status
            statusElement.innerHTML = '<i class="fas fa-camera text-success"></i> Camera đã sẵn sàng - Hướng QR code vào khung quét';
            
            // Hide flashlight button for now (Html5Qrcode doesn't have easy flashlight control)
            const flashlightBtn = document.getElementById('flashlightBtn');
            flashlightBtn.style.display = 'none';
        } else {
            throw new Error('Không tìm thấy camera nào');
        }

    } catch (error) {
        console.error('Error starting QR scanner:', error);
        const statusElement = document.getElementById('scannerStatus');
        statusElement.innerHTML = '<i class="fas fa-exclamation-triangle text-danger"></i> Lỗi: ' + error.message;
        toastr.error('Không thể khởi động camera: ' + error.message);
    }
}

function stopQRScanner() {
    try {
        if (html5QrCode) {
            html5QrCode.stop().then(() => {
                html5QrCode.clear();
                html5QrCode = null;
            }).catch((err) => {
                console.error('Error stopping scanner:', err);
                html5QrCode = null;
            });
        }
        
        // Reset flashlight
        isFlashlightOn = false;
        
        // Hide flashlight button
        const flashlightBtn = document.getElementById('flashlightBtn');
        flashlightBtn.style.display = 'none';
        
        // Reset status
        const statusElement = document.getElementById('scannerStatus');
        statusElement.innerHTML = '<i class="fas fa-camera"></i> Đang khởi động camera...';
        
    } catch (error) {
        console.error('Error stopping QR scanner:', error);
    }
}

async function toggleFlashlight() {
    try {
        // Html5Qrcode doesn't have built-in flashlight control
        // This would require additional implementation
        toastr.info('Chức năng đèn pin chưa được hỗ trợ trong phiên bản này');
    } catch (error) {
        console.error('Error toggling flashlight:', error);
        toastr.error('Không thể điều khiển đèn pin');
    }
}

function handleQRResult(result) {
    try {
        console.log('QR Code detected:', result.data);
        
        // Parse Vietnamese CCCD QR code
        const parsedData = parseCCCDQRCode(result.data);
        
        if (parsedData) {
            // Fill form with parsed data
            fillCustomerForm(parsedData);
            
            // Close QR scanner modal
            stopQRScanner();
            const qrModal = bootstrap.Modal.getInstance(document.getElementById('qrScannerModal'));
            if (qrModal) {
                qrModal.hide();
            }
            
            toastr.success('Đã quét thành công thông tin CCCD!');
        } else {
            toastr.warning('QR code không phải là CCCD hợp lệ');
        }
        
    } catch (error) {
        console.error('Error handling QR result:', error);
        toastr.error('Lỗi xử lý QR code: ' + error.message);
    }
}

function parseCCCDQRCode(qrData) {
    try {
        // Vietnamese CCCD QR codes typically contain data separated by | or similar delimiters
        // Format: ID|Name|Date of Birth|Gender|Address|Date of Issue
        // Example: 123456789012|NGUYEN VAN A|01/01/1990|Nam|Ha Noi|01/01/2020
        
        // Remove any leading/trailing whitespace
        qrData = qrData.trim();
        
        // Try different separators commonly used in Vietnamese CCCD
        let parts = [];
        const separators = ['|', '\n', '\t', ';'];
        
        for (const separator of separators) {
            if (qrData.includes(separator)) {
                parts = qrData.split(separator);
                break;
            }
        }
        
        // If no separator found, try to parse as a continuous string
        if (parts.length < 4) {
            // Some CCCD QR codes use fixed-width fields
            return parseCCCDFixedWidth(qrData);
        }
        
        // Validate we have minimum required fields
        if (parts.length < 4) {
            console.warn('Invalid CCCD QR format - insufficient fields');
            return null;
        }
        
        // Extract and clean data
        const cccd = parts[0]?.trim();
        const hoTen = parts[1]?.trim();
        const ngaySinh = parts[2]?.trim();
        const gioiTinh = parts[3]?.trim();
        
        // Validate CCCD number (12 digits)
        if (!cccd || !/^\d{12}$/.test(cccd)) {
            console.warn('Invalid CCCD number format');
            return null;
        }
        
        // Validate name
        if (!hoTen || hoTen.length < 2) {
            console.warn('Invalid name format');
            return null;
        }
        
        // Convert date format if needed
        const formattedDate = formatDateForDisplay(ngaySinh);
        
        // Normalize gender
        const normalizedGender = normalizeGender(gioiTinh);
        
        return {
            cccd: cccd,
            hoTen: hoTen,
            ngaySinh: formattedDate,
            gioiTinh: normalizedGender
        };
        
    } catch (error) {
        console.error('Error parsing CCCD QR code:', error);
        return null;
    }
}

function parseCCCDFixedWidth(qrData) {
    try {
        // Some CCCD use fixed-width format
        // This is a fallback parser for different formats
        
        // Look for patterns like 12-digit ID numbers
        const cccdMatch = qrData.match(/\d{12}/);
        if (!cccdMatch) {
            return null;
        }
        
        const cccd = cccdMatch[0];
        
        // Try to extract name (usually after CCCD, before date)
        const afterCCCD = qrData.substring(qrData.indexOf(cccd) + 12);
        const nameMatch = afterCCCD.match(/([A-ZĂÂÊÔƠƯĐ\s]{2,})/i);
        
        if (!nameMatch) {
            return null;
        }
        
        const hoTen = nameMatch[1].trim();
        
        // Try to find date patterns
        const datePatterns = [
            /\d{2}\/\d{2}\/\d{4}/,  // DD/MM/YYYY
            /\d{2}-\d{2}-\d{4}/,    // DD-MM-YYYY
            /\d{4}-\d{2}-\d{2}/,    // YYYY-MM-DD
            /\d{8}/                 // DDMMYYYY
        ];
        
        let ngaySinh = '';
        for (const pattern of datePatterns) {
            const match = afterCCCD.match(pattern);
            if (match) {
                ngaySinh = formatDateForDisplay(match[0]);
                break;
            }
        }
        
        // Try to find gender
        const genderKeywords = ['nam', 'nữ', 'male', 'female', 'M', 'F'];
        let gioiTinh = '';
        
        for (const keyword of genderKeywords) {
            if (afterCCCD.toLowerCase().includes(keyword.toLowerCase())) {
                gioiTinh = normalizeGender(keyword);
                break;
            }
        }
        
        return {
            cccd: cccd,
            hoTen: hoTen,
            ngaySinh: ngaySinh,
            gioiTinh: gioiTinh
        };
        
    } catch (error) {
        console.error('Error parsing fixed-width CCCD:', error);
        return null;
    }
}

function formatDateForDisplay(dateStr) {
    if (!dateStr) return '';
    
    try {
        // Remove any non-digit characters except / and -
        let cleanDate = dateStr.replace(/[^\d\/\-]/g, '');
        
        // Handle different date formats
        if (/^\d{8}$/.test(cleanDate)) {
            // DDMMYYYY format
            const day = cleanDate.substring(0, 2);
            const month = cleanDate.substring(2, 4);
            const year = cleanDate.substring(4, 8);
            return `${day}/${month}/${year}`;
        } else if (/^\d{4}-\d{2}-\d{2}$/.test(cleanDate)) {
            // YYYY-MM-DD format
            const parts = cleanDate.split('-');
            return `${parts[2]}/${parts[1]}/${parts[0]}`;
        } else if (/^\d{2}[\/-]\d{2}[\/-]\d{4}$/.test(cleanDate)) {
            // DD/MM/YYYY or DD-MM-YYYY format
            return cleanDate.replace(/-/g, '/');
        }
        
        return cleanDate;
    } catch (error) {
        console.error('Error formatting date:', error);
        return dateStr;
    }
}

function normalizeGender(genderStr) {
    if (!genderStr) return '';
    
    const gender = genderStr.toLowerCase().trim();
    
    if (gender.includes('nam') || gender === 'm' || gender === 'male') {
        return 'Nam';
    } else if (gender.includes('nữ') || gender === 'f' || gender === 'female') {
        return 'Nữ';
    }
    
    return '';
}

function fillCustomerForm(customerData) {
    try {
        // Find the form inputs
        const hoTenInput = document.querySelector('input[name="hoTen"]');
        const cccdInput = document.querySelector('input[name="cccd"]');
        const gioiTinhSelect = document.querySelector('select[name="gioiTinh"]');
        const ngaySinhInput = document.querySelector('input[name="ngaySinh"]');
        
        // Fill the form fields
        if (hoTenInput && customerData.hoTen) {
            hoTenInput.value = customerData.hoTen;
            hoTenInput.dispatchEvent(new Event('input', { bubbles: true }));
        }
        
        if (cccdInput && customerData.cccd) {
            cccdInput.value = customerData.cccd;
            cccdInput.dispatchEvent(new Event('input', { bubbles: true }));
        }
        
        if (gioiTinhSelect && customerData.gioiTinh) {
            gioiTinhSelect.value = customerData.gioiTinh;
            gioiTinhSelect.dispatchEvent(new Event('change', { bubbles: true }));
        }
        
        if (ngaySinhInput && customerData.ngaySinh) {
            ngaySinhInput.value = customerData.ngaySinh;
            ngaySinhInput.dispatchEvent(new Event('input', { bubbles: true }));
        }
        
        // Highlight filled fields briefly
        const fields = [hoTenInput, cccdInput, gioiTinhSelect, ngaySinhInput];
        fields.forEach(field => {
            if (field && field.value) {
                field.style.transition = 'all 0.3s ease';
                field.style.backgroundColor = '#e8f5e8';
                field.style.borderColor = '#28a745';
                
                setTimeout(() => {
                    field.style.backgroundColor = '';
                    field.style.borderColor = '';
                }, 2000);
            }
        });
        
        console.log('Customer form filled with:', customerData);
        
    } catch (error) {
        console.error('Error filling customer form:', error);
    }
}