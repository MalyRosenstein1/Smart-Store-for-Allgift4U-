import { Component, ViewChild, ElementRef, HostListener, AfterViewChecked, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryOrderService } from '../../services/inventory-order.service';
import { AuthService } from '../../services/auth.service';
import { ChatHistoryService, StoredMessage, ChatSession } from '../../services/chat-history.service';

interface ChatMessage extends StoredMessage {}

type ChatMode = 'menu' | 'order' | 'stock' | 'check';

const EMOJI_LIST = [
  '😀','😁','😂','🤣','😃','😄','😅','😆','😉','😊',
  '😋','😎','😍','🥰','😘','🤔','😐','😑','😶','🙄',
  '😏','😣','😥','😮','🤐','😯','😪','😫','🥱','😴',
  '😌','😛','😜','😝','🤤','😒','😓','😔','😕','🙃',
  '🤑','😲','🙁','😖','😞','😟','😤','😢','😭','😦',
  '😧','😨','😩','🤯','😬','😰','😱','🥵','🥶','😳',
  '🤪','😵','🥴','😷','🤒','🤕','🤢','🤮','🤧','😇',
  '🥳','🥸','🤠','🤡','🤥','🤫','🤭','🧐','🤓','😈',
  '👍','👎','👋','🙌','👏','🤝','🙏','❤️','🔥','✅',
  '❌','⚠️','📦','🎁','🛒','💰','📈','📉','🏪','🚚'
];

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.scss']
})
export class ChatComponent implements AfterViewChecked {
  @ViewChild('messagesContainer') messagesContainer!: ElementRef;
  @ViewChild('imgFileInput') imgFileInput!: ElementRef<HTMLInputElement>;

  messages: ChatMessage[] = [];
  sessions: ChatSession[] = [];
  currentSessionId: string | null = null;
  searchQuery = '';
  input = '';
  loading = false;
  imagePreview: string | null = null;
  mode: ChatMode = 'menu';
  
  readonly emojiList = EMOJI_LIST;
  showInputEmoji = false;
  
  private shouldScrollToBottom = false;
  private pendingImage: { base64: string; mimeType: string; fileName: string } | null = null;

  constructor(
    private inventoryService: InventoryOrderService,
    public auth: AuthService,
    private historyService: ChatHistoryService,
    private cdr: ChangeDetectorRef
  ) {
    this.initializeSession();
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
      this.shouldScrollToBottom = false;
    }
  }

  private initializeSession(): void {
    this.sessions = this.historyService.getSessions(this.auth.username);
    this.currentSessionId = this.historyService.createSession(this.auth.username);
    this.mode = 'menu';
    this.messages = [
      { role: 'bot', text: 'היי! 👋 מה תרצה לעשות?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי\n3️⃣ בדיקת מלאי', timestamp: this.getCurrentTime() }
    ];
    this.historyService.saveMessages(this.currentSessionId, this.messages);
    this.sessions = this.historyService.getSessions(this.auth.username, this.currentSessionId);
    this.shouldScrollToBottom = true;
  }

  get filteredSessions(): ChatSession[] {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return this.sessions;
    return this.sessions.filter(s => 
      s.username.toLowerCase().includes(q) ||
      s.messages.some(m => m.text.toLowerCase().includes(q))
    );
  }

  loadSession(session: ChatSession): void {
    this.currentSessionId = session.id;
    this.messages = [...session.messages];
    this.shouldScrollToBottom = true;
  }

  getLastMessage(session: ChatSession): string {
    const last = session.messages[session.messages.length - 1];
    return last ? last.text.substring(0, 35) + (last.text.length > 35 ? '...' : '') : 'No messages';
  }

  send(): void {
    const text = this.input.trim();
    
    if ((!text && !this.pendingImage) || this.loading) return;
    
    if (this.pendingImage) {
      this.sendImageMessage(text);
      return;
    }

    this.messages.push({ role: 'user', text, timestamp: this.getCurrentTime() });
    this.messages = [...this.messages];
    if (this.currentSessionId) {
      this.historyService.saveMessages(this.currentSessionId, this.messages);
    }
    this.input = '';
    this.loading = true;
    this.shouldScrollToBottom = true;

    if (this.mode === 'menu') {
      if (text === '1' || text.includes('הזמנה')) {
        this.mode = 'order';
        this.addBotMessage('📦 כתוב את שם ההזמנה:');
      } else if (text === '2' || text.includes('מלאי')) {
        this.mode = 'stock';
        this.addBotMessage('📝 עדכון מלאי:\n\n➕ להוסיף כמות: כתוב **+20 צלופנים**\n🔄 לקבוע כמות חדשה: כתוב **=20 צלופנים**');
      } else if (text === '3' || text.includes('בדיקה')) {
        this.mode = 'check';
        this.addBotMessage('🔍 כתוב שם הזמנה לבדיקה, לדוגמה: חבילה לחתן');
      } else {
        this.addBotMessage('אנא בחר:\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי\n3️⃣ בדיקת מלאי');
      }
      this.loading = false;
      return;
    }

    if (this.mode === 'order') {
      if (text === 'תפריט') {
        this.mode = 'menu';
        this.addBotMessage('מה תרצה לעשות?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי');
        this.loading = false;
        return;
      }
      this.inventoryService.processOrder(text).subscribe({
        next: (res: any) => {
          const itemLines = (res.items ?? []).map((i: any) =>
            `• ${i.productName}: הוסר ${i.quantityRemoved}, נשאר ${i.newQuantity}`).join('\n');
          this.addBotMessage(`✅ ההזמנה "${res.orderName}" אפשרית ועובדה!\n\n${itemLines}\n\nמה תרצה לעשות עכשיו?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי`);
          this.mode = 'menu';
        },
        error: (err: any) => {
          let errorMsg = 'שגיאה לא ידועה';
          if (err.error?.error) errorMsg = err.error.error;
          else if (typeof err.error === 'string') errorMsg = err.error;
          else if (err.message) errorMsg = err.message;
          this.addBotMessage(`❌ ${errorMsg}\n\nנסה שם הזמנה אחר או חזור לתפריט (כתוב: תפריט)`);
        }
      });
      return;
    }

    if (this.mode === 'stock') {
      // +20 צלופנים = הוסף | =20 צלופנים = קבע
      const match = text.match(/^([+=])(\d+)\s+(.+)$/);
      if (!match) {
        this.addBotMessage('❌ פורמט לא תקין.\n➕ להוסיף: **+20 צלופנים**\n🔄 לקבוע: **=20 צלופנים**');
        this.loading = false;
        return;
      }
      const operator = match[1];
      const quantity = parseInt(match[2]);
      const productName = match[3].trim();

      this.inventoryService.getProducts().subscribe({
        next: (products: any[]) => {
          const product = products.find((p: any) =>
            p.productName.includes(productName) || productName.includes(p.productName));
          if (!product) {
            this.addBotMessage(`❌ המוצר "${productName}" לא נמצא במערכת.`);
            return;
          }
          const newQty = operator === '+'
            ? (product.currentQuantity ?? 0) + quantity
            : quantity;
          this.inventoryService.updateProductStock(product.productID, newQty).subscribe({
            next: (res: any) => {
              const action = operator === '+' ? `הוספו ${quantity}, סה"כ` : 'נקבע';
              this.addBotMessage(`✅ המלאי של "${res.productName}" ${action} ${res.newQuantity} יחידות\n\nמה תרצה לעשות עכשיו?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי`);
              this.mode = 'menu';
            },
            error: () => this.addBotMessage('❌ שגיאה בעדכון המלאי')
          });
        },
        error: () => this.addBotMessage('❌ שגיאה בטעינת המוצרים')
      });
      return;
    }

    if (this.mode === 'check') {
      this.inventoryService.getOrderStock(text).subscribe({
        next: (res: any) => {
          if (!res || !res.items || res.items.length === 0) {
            this.addBotMessage(`❌ הזמנה "${text}" לא נמצאה.\n\nמה תרצה לעשות?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי\n3️⃣ בדיקת מלאי`);
            this.mode = 'menu';
            return;
          }
          const lines = res.items.map((i: any) => {
            const status = i.currentQuantity < i.quantityRequired ? '❌ אין מספיק' : '✅ יש';
            return `${status} ${i.productName}: יש ${i.currentQuantity}, נדרש ${i.quantityRequired}`;
          }).join('\n');
          const canFulfill = res.items.every((i: any) => i.currentQuantity >= i.quantityRequired);
          const summary = canFulfill ? '✅ אפשר לעבד את ההזמנה!' : '❌ אין מספיק מלאי לעיבוד.';
          this.addBotMessage(`📦 מלאי להזמנה "${res.orderName}":\n\n${lines}\n\n${summary}\n\nמה תרצה לעשות?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי\n3️⃣ בדיקת מלאי`);
          this.mode = 'menu';
        },
        error: (err: any) => {
          const errorMsg = err.error?.error || err.message || 'שגיאה';
          this.addBotMessage(`❌ ${errorMsg}\n\nמה תרצה לעשות?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי\n3️⃣ בדיקת מלאי`);
          this.mode = 'menu';
        }
      });
      return;
    }

  }

  private sendImageMessage(caption: string): void {
    if (!this.pendingImage) return;
    
    const img = this.pendingImage;
    this.messages.push({ 
      role: 'user', 
      text: caption || '📸 תמונה נשלחה לניתוח',
      timestamp: this.getCurrentTime()
    });
    this.messages = [...this.messages];
    if (this.currentSessionId) {
      this.historyService.saveMessages(this.currentSessionId, this.messages);
    }
    this.input = '';
    this.loading = true;
    this.clearImage();
    this.shouldScrollToBottom = true;

    this.inventoryService.processImage(img.base64, img.mimeType, img.fileName).subscribe({
      next: (res: any) => {
        const msg = res.botMessage || res.text || 'לא הצלחתי לנתח.';
        this.addBotMessage(`${msg}\n\nמה תרצה לעשות עכשיו?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי`);
        this.mode = 'menu';
      },
      error: (err: any) => {
        const errorMsg = err.error?.error || err.message || 'שגיאה בעיבוד תמונה';
        this.addBotMessage(`❌ ${errorMsg}`);
      }
    });
  }

  private addBotMessage(text: string, items: any[] = [], recommendations: any[] = [], warnings: string[] = []): void {
    this.messages = [...this.messages, {
      role: 'bot',
      text,
      items,
      recommendations,
      warnings,
      timestamp: this.getCurrentTime()
    }];
    this.loading = false;
    this.shouldScrollToBottom = true;
    if (this.currentSessionId) {
      this.historyService.saveMessages(this.currentSessionId, this.messages);
      this.sessions = this.historyService.getSessions(this.auth.username, this.currentSessionId);
    }
    this.cdr.detectChanges();
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      const [header, base64] = dataUrl.split(',');
      this.pendingImage = { 
        base64, 
        mimeType: header.match(/:(.*?);/)?.[1] ?? 'image/jpeg',
        fileName: file.name
      };
      this.imagePreview = dataUrl;
    };
    reader.readAsDataURL(file);
  }

  clearImage(): void {
    this.pendingImage = null;
    this.imagePreview = null;
    if (this.imgFileInput) {
      this.imgFileInput.nativeElement.value = '';
    }
  }

  toggleInputEmoji(event: MouseEvent): void {
    event.stopPropagation();
    this.showInputEmoji = !this.showInputEmoji;
  }

  addEmojiToInput(emoji: string): void {
    this.input += emoji;
    this.showInputEmoji = false;
  }

  @HostListener('document:click')
  closeEmojiPicker(): void {
    this.showInputEmoji = false;
  }

  onEnter(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  private scrollToBottom(): void {
    const container = this.messagesContainer?.nativeElement;
    if (container) {
      setTimeout(() => {
        container.scrollTop = container.scrollHeight;
      }, 0);
    }
  }

  private getCurrentTime(): string {
    return new Date().toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' });
  }

  trackBySessionId(index: number, session: ChatSession): string {
    return session.id;
  }
}
