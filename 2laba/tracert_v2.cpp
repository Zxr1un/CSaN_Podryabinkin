#include <iostream>
#include <cctype>
#include <chrono>


#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>

#pragma comment(lib, "ws2_32.lib")

using namespace std;

void check_input(char* str);
void tracert_by_IP( char* ip);
char* get_ip(char* domain);
char* get_domain(char* addr_str);

bool check_domain = false;
bool not_cmd = false;
int main(int argc, char* argv[])
{
    if (argc < 2) {
        cout << "No arguments in request\n";
        cout << "Or started not from system cmd. Write command here: ";
        // Вводим всю строку целиком
        char input[512];
        cin.getline(input, sizeof(input));

        // Парсим введенную строку
        char* tokens[10];  // максимум 10 аргументов
        int token_count = 0;

        char* context = nullptr;
        char* token = strtok_s(input, " ", &context);

        while (token != nullptr && token_count < 10) {
            // Пропускаем пустые токены (несколько пробелов подряд)
            if (strlen(token) > 0) {
                tokens[token_count] = token;
                token_count++;
            }
            token = strtok_s(nullptr, " ", &context);
        }

        if (token_count == 0) {
            cout << "No command entered\n";
            return 1;
        }
        if (token_count > 0) {
            if (strcmp(tokens[0], "tracert_v2") != 0) {
                cout << "No command entered\n";
                return 1;
            }
        }

        // Обрабатываем как обычные аргументы командной строки
        if (token_count >= 2 && strcmp(tokens[1], "-d") == 0) {
            not_cmd = true;
            check_domain = true;
            check_input(tokens[2]);
            
        }
        else {
            not_cmd = true;
            check_input(tokens[1]);
 
        }
    }
    else if (argc > 2) {
        if (strcmp(argv[1],"-d") == 0) {
            check_domain = true;
            check_input(argv[2]);
        }
        else check_input(argv[1]);
    }
    else check_input(argv[1]);

    return 0;
}

void check_input(char* str)
{
    if (isdigit(str[0])) {
        tracert_by_IP(str);
    }
    else {
        char* ip = get_ip(str);
        if (ip == nullptr) {
            cout << "abort";
            return;
        }
        tracert_by_IP(ip);
    }
}


struct ICMPHeader
{
    uint8_t type;
    uint8_t code;
    uint16_t checksum;
    uint16_t id;
    uint16_t sequence;
};


uint16_t checksum(uint16_t* buffer, int size)
{
    unsigned long sum = 0;

    while (size > 1)
    {
        sum += *buffer++;
        size -= 2;
    }

    if (size)
        sum += *(uint8_t*)buffer;

    sum = (sum >> 16) + (sum & 0xffff);
    sum += (sum >> 16);

    return (uint16_t)(~sum);
}



// ICMP

void tracert_by_IP( char* ip)
{
    WSADATA wsa;
    WSAStartup(MAKEWORD(2, 2), &wsa);

    SOCKET sock = socket(AF_INET, SOCK_RAW, IPPROTO_ICMP);

    int timeout = 3000; // 7 секунд
    setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, (char*)&timeout, sizeof(timeout));

    if (sock == INVALID_SOCKET)
    {
        cout << "Socket creation failed\n";
        return;
    }

    sockaddr_in dest{};
    dest.sin_family = AF_INET;
    inet_pton(AF_INET, ip, &dest.sin_addr);

    int MAX_TTL = 30;
    int PACKETS_PER_HOP = 3;

    char sendbuf[64];
    char recvbuf[1024];

    ICMPHeader* icmp = (ICMPHeader*)sendbuf;

    icmp->type = 8; // Echo request
    icmp->code = 0;
    icmp->id = (uint16_t)GetCurrentProcessId();
    

    for (int ttl = 1; ttl <= MAX_TTL; ttl++)
    {
        setsockopt(sock, IPPROTO_IP, IP_TTL, (char*)&ttl, sizeof(ttl));
        bool dest_is_achieved = false;
        cout << ttl << ".\t";

        for (int p = 0; p < PACKETS_PER_HOP; p++)
        {
            icmp->sequence = htons((ttl - 1) * 3 + p);
            icmp->checksum = 0;
            icmp->checksum = checksum((uint16_t*)icmp, sizeof(ICMPHeader));

            auto start = std::chrono::high_resolution_clock::now();
            //----------------------------------------------------------------------------------
            sendto(sock, sendbuf, sizeof(ICMPHeader), 0, (sockaddr*)&dest,  sizeof(dest));
            sockaddr_in reply_addr{};
            int addr_len = sizeof(reply_addr);
            //------------------------------------------------------------------------------------
            int attemps = 64;
            bool megabreak = false;

            char* domain = nullptr;
            char* addr = nullptr;

            while (attemps > 0 and !megabreak) {
                int ret = recvfrom(sock, recvbuf, sizeof(recvbuf), 0, (sockaddr*)&reply_addr, &addr_len);
                ICMPHeader* recv_icmp = (ICMPHeader*)(recvbuf + 20);
                if (ret == SOCKET_ERROR)
                {
                    cout << "*\t\t";
                    megabreak = true;
                    if (p == PACKETS_PER_HOP - 1 and addr != nullptr) {
                        cout << "\t" << addr;
                        if (domain != nullptr) cout << " (" << domain << ")";
                        cout << "\t\t\t";
                        printf("0x%04X\t", ntohs(icmp->checksum));
                        if (strcmp(addr, ip) == 0) dest_is_achieved = true;
                    }
                    
                    continue;
                }
                int seq0 = htons(icmp->sequence);
                if (recv_icmp->type == 0) {
                    int recvseq0 = htons(recv_icmp->sequence);
                    if (seq0 != recvseq0) {
                        attemps--;
                        cout << "r";
                        continue;
                    }
                    cout << " (" << htons(icmp->sequence) << "|" << htons(recv_icmp->sequence) << ") ";
                    if (seq0 != recvseq0) {
                        attemps--;
                        continue;
                    }
                }
                else {
                    recv_icmp = (ICMPHeader*)(recvbuf + 20 + 8 + 20);
                    int recvseq0 = htons(recv_icmp->sequence);
                    if (seq0 != recvseq0) {
                        attemps--;
                        cout << "r";
                        continue;
                    }
                    cout << " (" << htons(icmp->sequence) << "|" << htons(recv_icmp->sequence) << ") ";
                }


                auto end = std::chrono::steady_clock::now();
                double ms = std::chrono::duration<double, std::milli>(end - start).count();

                cout << (int)ms << "ms" << "\t";

                char addr_str[INET_ADDRSTRLEN];
                addr = addr_str;

                inet_ntop(AF_INET, &reply_addr.sin_addr, addr_str, sizeof(addr_str));
                if (check_domain) domain = get_domain(addr_str);

                if (p == PACKETS_PER_HOP - 1) {
                    cout << "\t" << addr;
                    if (domain != nullptr) cout << " (" << domain << ")";
                    cout << "\t\t\t";
                    printf("0x%04X\t", ntohs(icmp->checksum));
                }
                if (strcmp(addr, ip) == 0) dest_is_achieved = true;
                break;
            }
            
        }
        if (dest_is_achieved) {
            if (not_cmd) {
                cout << "\n\nWrite and press Enter, to escape\n\n";
                char* a = new char[256];
                cin >> a;
            }
            break;
        }
        cout << std::endl;
    }
    closesocket(sock);
    WSACleanup();
}




// DNS

#pragma pack(push,1) //чтоб в памяти ничего не резало
struct DNSHeader
{
    uint16_t id;
    uint16_t flags;
    uint16_t qdcount;
    uint16_t ancount;
    uint16_t nscount;
    uint16_t arcount;
};
struct DNSQuestion
{
    uint16_t qtype;
    uint16_t qclass;
};
#pragma pack(pop)


//чтоб тому, кто так кодирует пусто было...
void encode_domain( char* domain, char* dns)
{
    int lock = 0;
    int len = strlen(domain);
    for (int i = 0; i < len; i++)
    {
        if (domain[i] == '.')
        {
            *dns = i - lock;
            dns++;
            for (; lock < i; lock++) *dns++ = domain[lock];
            lock++;
        }
    }
    *dns++ = len - lock;
    for (; lock < len; lock++) *dns++ = domain[lock];
    *dns = 0;
    dns++;
}


char* get_ip(char* domain)
{
    int timeout = 3000;

    static char ip_str[INET_ADDRSTRLEN];
    memset(ip_str, 0, sizeof(ip_str));

    // Инициализация Winsock
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0)
    {
        cout << "WSAStartup failed\n";
        return nullptr;
    }

    SOCKET sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (sock == INVALID_SOCKET)
    {
        cout << "DNS socket error: " << WSAGetLastError() << "\n";
        WSACleanup();
        return nullptr;
    }

    // Установка таймаута
    if (setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, (char*)&timeout, sizeof(timeout)) == SOCKET_ERROR)
    {
        cout << "Setsockopt error: " << WSAGetLastError() << "\n";
    }

    sockaddr_in dns{};
    dns.sin_family = AF_INET;
    dns.sin_port = htons(53);
    inet_pton(AF_INET, "8.8.8.8", &dns.sin_addr);

    char buffer[512] = { 0 }; // Полная инициализация нулями

    DNSHeader* dns_hdr = (DNSHeader*)buffer;
    dns_hdr->id = htons(0x1234);
    dns_hdr->flags = htons(0x0100); // Стандартный запрос
    dns_hdr->qdcount = htons(1);

    char* qname = (char*)(buffer + sizeof(DNSHeader));
    encode_domain(domain, qname);

    DNSQuestion* qinfo = (DNSQuestion*)(qname + strlen(qname) + 1);
    qinfo->qtype = htons(1); // A record
    qinfo->qclass = htons(1); // IN class

    int packet_size = sizeof(DNSHeader) + strlen(qname) + 1 + sizeof(DNSQuestion);

    cout << "Sending DNS query for domain: " << domain << "\n";

    if (sendto(sock, buffer, packet_size, 0, (sockaddr*)&dns, sizeof(dns)) == SOCKET_ERROR)
    {
        cout << "Sendto failed: " << WSAGetLastError() << "\n";
        closesocket(sock);
        WSACleanup();
        return nullptr;
    }

    sockaddr_in reply{};
    int reply_len = sizeof(reply);

    int recv_size = recvfrom(sock, buffer, sizeof(buffer), 0, (sockaddr*)&reply, &reply_len);
    if (recv_size == SOCKET_ERROR)
    {
        cout << "Recvfrom failed: " << WSAGetLastError() << "\n";
        closesocket(sock);
        WSACleanup();
        return nullptr;
    }

    if (recv_size < sizeof(DNSHeader))
    {
        cout << "Response too small\n";
        closesocket(sock);
        WSACleanup();
        return nullptr;
    }

    DNSHeader* resp = (DNSHeader*)buffer;
    if (ntohs(resp->ancount) == 0)
    {
        cout << "No DNS answer records\n";
        closesocket(sock);
        WSACleanup();
        return nullptr;
    }

    // Парсинг ответа
    unsigned char* reader = (unsigned char*)buffer + sizeof(DNSHeader);

    // Пропускаем имя в вопросе (с учетом возможной компрессии)
    while (*reader != 0)
    {
        if ((*reader & 0xC0) == 0xC0) // Если компрессия
        {
            reader += 2;
            break;
        }
        reader += *reader + 1;
    }
    if (*reader == 0) reader++; // Пропускаем нулевой байт

    // Пропускаем вопрос (type и class)
    reader += 4;

    // Ищем A запись в ответах
    bool found = false;
    for (int i = 0; i < ntohs(resp->ancount); i++)
    {
        // Проверяем границы буфера
        if (reader >= (unsigned char*)buffer + recv_size)
            break;

        // Пропускаем имя ответа (с учетом компрессии)
        if ((*reader & 0xC0) == 0xC0)
        {
            reader += 2;
        }
        else
        {
            while (*reader != 0)
            {
                reader += *reader + 1;
            }
            reader++;
        }

        // Проверяем достаточно ли места для type, class, ttl, rdlength
        if (reader + 10 > (unsigned char*)buffer + recv_size)
            break;

        uint16_t type = ntohs(*(uint16_t*)reader);
        reader += 2; // type
        reader += 2; // class
        reader += 4; // ttl

        uint16_t rdlength = ntohs(*(uint16_t*)reader);
        reader += 2;

        // Если это A запись (type 1) и длина данных 4 байта (IPv4)
        if (type == 1 && rdlength == 4)
        {
            if (reader + 4 <= (unsigned char*)buffer + recv_size)
            {
                inet_ntop(AF_INET, reader, ip_str, sizeof(ip_str));
                cout << "Found IP: " << ip_str << "\n";
                found = true;
                break;
            }
        }

        // Переходим к следующему ответу
        reader += rdlength;
    }

    closesocket(sock);
    WSACleanup();

    if (found)
        return ip_str;
    else
    {
        cout << "No A record found\n";
        return nullptr;
    }
}

char* get_domain(char* ip)
{
    int timeout = 3000;
    static char domain[256];
    memset(domain, 0, sizeof(domain));

    int a, b, c, d;
    if (sscanf_s(ip, "%d.%d.%d.%d", &a, &b, &c, &d) != 4) return nullptr;
    if (a < 0 || a > 255 || b < 0 || b > 255 || c < 0 || c > 255 || d < 0 || d > 255) return nullptr;

    char reversed[256];
    sprintf_s(reversed, sizeof(reversed), "%d.%d.%d.%d.in-addr.arpa", d, c, b, a);


    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) return nullptr;
    SOCKET sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (sock == INVALID_SOCKET) {
        WSACleanup();
        return nullptr;
    }
    setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, (char*)&timeout, sizeof(timeout));



    sockaddr_in dns{};
    dns.sin_family = AF_INET;
    dns.sin_port = htons(53);
    inet_pton(AF_INET, "8.8.8.8", &dns.sin_addr);
    char buffer[512]{};


    DNSHeader* dns_hdr = (DNSHeader*)buffer;
    dns_hdr->id = htons(0x4321);
    dns_hdr->flags = htons(0x0100);
    dns_hdr->qdcount = htons(1);
    char* qname = (char*)(buffer + sizeof(DNSHeader));
    encode_domain(reversed, qname);
    DNSQuestion* qinfo = (DNSQuestion*)(qname + strlen(qname) + 1);
    qinfo->qtype = htons(12); // PTR
    qinfo->qclass = htons(1);

    int query_size = sizeof(DNSHeader) + (strlen(qname) + 1) + sizeof(DNSQuestion);
    if (sendto(sock, buffer, query_size, 0, (sockaddr*)&dns, sizeof(dns)) == SOCKET_ERROR) {
        closesocket(sock);
        WSACleanup();
        return nullptr;
    }

    sockaddr_in reply{};
    int len = sizeof(reply);
    int recv_size = recvfrom(sock, buffer, sizeof(buffer), 0, (sockaddr*)&reply, &len);
    if (recv_size == SOCKET_ERROR) {
        closesocket(sock);
        WSACleanup();
        return nullptr;
    }

    DNSHeader* resp = (DNSHeader*)buffer;
    if (ntohs(resp->ancount) == 0) {
        closesocket(sock);
        WSACleanup();
        return nullptr;
    }

    // Пропускаем заголовок запроса
    unsigned char* reader = (unsigned char*)buffer + sizeof(DNSHeader);

    // Пропускаем имя в запросе
    while (*reader != 0) {
        if ((*reader & 0xC0) == 0xC0) {
            reader += 2; // байты
            break;
        }
        reader += *reader + 1;
    }
    if (*reader == 0) reader++; // пропускаем нуль-терминатор

    // Пропускаем вопрос
    reader += 4; // qtype + qclass

    // Обработка ответов
    int answer_count = ntohs(resp->ancount);
    domain[0] = '\0'; // очищаем буфер
    for (int i = 0; i < answer_count; i++) {
        if (reader >= (unsigned char*)buffer + recv_size) break;
        // Обработка имени в ответе (с учетом компрессии)
        if ((*reader & 0xC0) == 0xC0) {
            reader += 2;
        }
        else {
            while (*reader != 0) {
                reader += *reader + 1;
            }
            reader++;
        }
        // Читаем type, class, ttl
        if (reader + 10 > (unsigned char*)buffer + recv_size) break;
        uint16_t type = ntohs(*(uint16_t*)reader);
        reader += 2;
        reader += 2; // class
        reader += 4; // ttl

        uint16_t rdlength = ntohs(*(uint16_t*)reader);
        reader += 2;

        // Проверяем, что это PTR запись
        if (type == 12) { // PTR
            unsigned char* rdata = reader;
            int pos = 0;

            while (pos < rdlength && rdata < (unsigned char*)buffer + recv_size) {
                if (*rdata == 0) break;

                if ((*rdata & 0xC0) == 0xC0) {
                    // Обработка компрессии в ответе
                    int offset = ((*rdata & 0x3F) << 8) | *(rdata + 1);
                    unsigned char* compressed = (unsigned char*)buffer + offset;

                    while (*compressed != 0 && pos < sizeof(domain) - 1) {
                        int len = *compressed++;
                        for (int j = 0; j < len && pos < sizeof(domain) - 1; j++) {
                            domain[pos++] = *compressed++;
                        }
                        if (*compressed != 0) domain[pos++] = '.';
                    }
                    rdata += 2;
                }
                else {
                    int label_len = *rdata;
                    rdata++;
                    for (int j = 0; j < label_len && pos < sizeof(domain) - 1; j++) {
                        domain[pos++] = *rdata++;
                    }
                    if (*rdata != 0 && pos < sizeof(domain) - 1) {
                        domain[pos++] = '.';
                    }
                }
            }

            if (pos > 0 && domain[pos - 1] == '.') {
                domain[pos - 1] = '\0';
            }
            else {
                domain[pos] = '\0';
            }

            // Если нашли PTR запись, выходим
            break;
        }
        else {
            reader += rdlength;
        }
    }

    closesocket(sock);
    WSACleanup();

    return domain[0] != '\0' ? domain : nullptr;
}