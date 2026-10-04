import { Component, ViewChild, ElementRef, HostListener, AfterViewChecked, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventoryOrderService } from '../../services/inventory-order.service';
import { AuthService } from '../../services/auth.service';
import { ChatHistoryService, StoredMessage, ChatSession } from '../../services/chat-history.service';

interface ChatMessage extends StoredMessage {}

type ChatMode = 'menu' | 'order' | 'stock' | 'check';
type StockUpdateOperator = '+' | '-' | '=';

interface StockUpdateParseResult {
  operator: StockUpdateOperator;
  quantity: number;
  productName: string;
}

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
  private readonly productNameAliases: Record<string, string> = {
    'בירה': 'בירה בוטל',
    'בקבוק בירה': 'בירה בוטל',
    'כוס': 'כוס חד פעמית',
    'בר שוקולד': 'חטיף שוקולד',
    'ברים שוקולד': 'חטיף שוקולד',
    'חטיף שוקולד': 'חטיף שוקולד',
    'אריזה': 'אריזת מתנה',
    'קופסה': 'קופסת מתנה',
    'ברכה': 'ברכת מזל טוב',
    'צלופן': 'שקית צלופן'
  };

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
        this.addBotMessage('📝 עדכון מלאי:\n\n➕ להוסיף כמות: כתוב **+20 צלופנים**\n➖ להפחית כמות: כתוב **-20 צלופנים**\n🔄 לקבוע כמות חדשה: כתוב **=20 צלופנים**');
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
          const warningLines = (res.warnings ?? res.StockDepletionWarnings ?? [])
            .map((warning: string) => `⚠️ ${warning}`)
            .join('\n');
          const warningText = warningLines ? `\n\n${warningLines}` : '';
          const deliveryNotePdf = res.deliveryNotePdf ?? res.DeliveryNotePdf;
          const deliveryNoteFileName = res.deliveryNoteFileName ?? res.DeliveryNoteFileName;
          this.addBotMessage(
            `✅ ההזמנה "${res.orderName}" אפשרית ועובדה!\n\n${itemLines}${warningText}\n\nמה תרצה לעשות עכשיו?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי`,
            [],
            [],
            [],
            deliveryNotePdf,
            deliveryNoteFileName
          );
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
      const parsed = this.parseStockUpdateInput(text);
      if (!parsed) {
        this.addBotMessage('❌ פורמט לא תקין.\n➕ להוסיף: **+20 צלופנים**\n➖ להפחית: **-20 צלופנים**\n🔄 לקבוע: **=20 צלופנים**');
        this.loading = false;
        return;
      }

      this.inventoryService.getProducts().subscribe({
        next: (products: any[]) => {
          const product = this.findProductByName(products, parsed.productName);
          if (!product) {
            this.addBotMessage(`❌ המוצר "${parsed.productName}" לא נמצא במערכת.`);
            return;
          }

          const productId = this.getProductID(product);
          const currentQuantity = this.getProductQuantity(product);
          if (!productId) {
            this.addBotMessage('❌ לא ניתן לעדכן את המלאי כי מזהה המוצר חסר.');
            return;
          }

          const newQty = parsed.operator === '+'
            ? currentQuantity + parsed.quantity
            : parsed.operator === '-'
              ? Math.max(0, currentQuantity - parsed.quantity)
              : parsed.quantity;

          this.inventoryService.updateProductStock(productId, newQty).subscribe({
            next: (res: any) => {
              const action = parsed.operator === '+'
                ? `הוספו ${parsed.quantity}, סה"כ`
                : parsed.operator === '-'
                  ? `הופחתו ${parsed.quantity}, סה"כ`
                  : 'נקבע';
              const updatedProductName = res.productName ?? res.ProductName ?? this.getProductName(product);
              this.addBotMessage(`✅ המלאי של "${updatedProductName}" ${action} ${res.newQuantity ?? res.NewQuantity} יחידות\n\nמה תרצה לעשות עכשיו?\n1️⃣ עיבוד הזמנה\n2️⃣ עדכון מלאי`);
              this.mode = 'menu';
            },
            error: () => this.addBotMessage('❌ שגיאה בעדכון המלאי')
          });
        },
        error: (err: any) => {
          const errorMsg = err.error?.error || err.message || 'שגיאה בטעינת המוצרים';
          this.addBotMessage(`❌ ${errorMsg}`);
        }
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

  private addBotMessage(text: string, items: any[] = [], recommendations: any[] = [], warnings: string[] = [], deliveryNotePdf?: string, deliveryNoteFileName?: string): void {
    this.messages = [...this.messages, {
      role: 'bot',
      text,
      items,
      recommendations,
      warnings,
      deliveryNotePdf,
      deliveryNoteFileName,
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

  downloadDeliveryNote(message: ChatMessage): void {
    const base64 = message.deliveryNotePdf;
    if (!base64) return;

    const base64Data = base64.includes(',') ? base64.split(',')[1] : base64;
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array<number>(byteCharacters.length);

    for (let i = 0; i < byteCharacters.length; i++) {
      byteNumbers[i] = byteCharacters.charCodeAt(i);
    }

    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: 'application/pdf' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');

    link.href = url;
    link.download = 'DeliveryNote.pdf';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
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

  private normalizeText(value: string): string {
    return (value ?? '')
      .toString()
      .normalize('NFKD')
      .replace(/[\u0591-\u05BD\u05BF\u05C1-\u05C2\u05C4-\u05C5\u05C7]/g, '')
      .replace(/[\u200E\u200F]/g, '')
      .replace(/ך/g, 'כ')
      .replace(/ם/g, 'מ')
      .replace(/ן/g, 'נ')
      .replace(/ף/g, 'פ')
      .replace(/ץ/g, 'צ')
      .replace(/\s+/g, ' ')
      .trim()
      .toLowerCase();
  }

  private parseStockUpdateInput(text: string): StockUpdateParseResult | null {
    const cleaned = text.trim()
      .replace(/\*\*/g, '')
      .replace(/__/g, '')
      .replace(/[\u200E\u200F]/g, '')
      .replace(/\s+/g, ' ');
    const prefixMatch = cleaned.match(/^([+\-=])\s*(\d+)\s*(.+)$/u);
    if (prefixMatch) {
      return {
        operator: prefixMatch[1] as StockUpdateOperator,
        quantity: Number(prefixMatch[2]),
        productName: prefixMatch[3].trim().replace(/[.!?:]+$/u, '')
      };
    }

    const addMatch = cleaned.match(/^(?:הוסף|הוספת|להוסיף|הוספה)\s+(\d+)\s*(.+)$/u);
    if (addMatch) {
      return { operator: '+', quantity: Number(addMatch[1]), productName: addMatch[2].trim().replace(/[.!?:]+$/u, '') };
    }

    const subtractMatch = cleaned.match(/^(?:הפחת|הפחתי|להפחית|הפחתה|החסר|הסר)\s+(\d+)\s*(.+)$/u);
    if (subtractMatch) {
      return { operator: '-', quantity: Number(subtractMatch[1]), productName: subtractMatch[2].trim().replace(/[.!?:]+$/u, '') };
    }

    const setMatch = cleaned.match(/^(?:קבע|לקבוע|שנה ל|לעדכן ל)\s+(\d+)\s*(.+)$/u);
    if (setMatch) {
      return { operator: '=', quantity: Number(setMatch[1]), productName: setMatch[2].trim().replace(/[.!?:]+$/u, '') };
    }

    return null;
  }

  private findProductByName(products: any[], productName: string): any | undefined {
    const normalizedProductName = this.normalizeText(productName);
    const canonicalProductName = this.getCanonicalProductName(normalizedProductName);
    if (!canonicalProductName) return undefined;

    const exactProduct = products.find((product: any) =>
      this.normalizeText(this.getProductName(product)) === canonicalProductName);

    if (exactProduct) return exactProduct;

    if (canonicalProductName !== normalizedProductName) {
      const fallbackExactProduct = products.find((product: any) =>
        this.normalizeText(this.getProductName(product)) === normalizedProductName);

      if (fallbackExactProduct) return fallbackExactProduct;
    }

    if (normalizedProductName.length < 3) return undefined;

    const storedNameContainsInput = products.filter((product: any) =>
      this.normalizeText(this.getProductName(product)).includes(normalizedProductName));

    if (storedNameContainsInput.length === 1) return storedNameContainsInput[0];

    const inputContainsStoredName = products.filter((product: any) =>
      normalizedProductName.includes(this.normalizeText(this.getProductName(product))));

    return inputContainsStoredName.length === 1 ? inputContainsStoredName[0] : undefined;
  }

  private getCanonicalProductName(normalizedProductName: string): string {
    return this.productNameAliases[normalizedProductName] ?? normalizedProductName;
  }

  private getProductName(product: any): string {
    return product?.productName ?? product?.ProductName ?? '';
  }

  private getProductID(product: any): number {
    return Number(product?.productID ?? product?.ProductID ?? 0);
  }

  private getProductQuantity(product: any): number {
    return Number(product?.currentQuantity ?? product?.CurrentQuantity ?? 0);
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
